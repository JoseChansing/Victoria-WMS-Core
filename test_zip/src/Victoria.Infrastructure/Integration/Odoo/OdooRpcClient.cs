using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public interface IOdooRpcClient
    {
        Task<int> AuthenticateAsync();
        Task<List<T>> SearchAndReadAsync<T>(string model, object[][] domain, string[] fields) where T : new();
        Task<bool> ExecuteAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null);
    }

    public class OdooRpcClient : IOdooRpcClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OdooRpcClient> _logger;
        private readonly string _url;
        private readonly string _db;
        private readonly string _user;
        private readonly string _apiKey;
        private int _uid = -1;

        public OdooRpcClient(HttpClient httpClient, IConfiguration config, ILogger<OdooRpcClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _url = (config["Odoo:Url"] ?? "").Trim();
            _db = (config["Odoo:Db"] ?? "").Trim();
            _user = (config["Odoo:User"] ?? "").Trim();
            _apiKey = (config["Odoo:ApiKey"] ?? "").Trim();

            if (string.IsNullOrEmpty(_url)) throw new ArgumentNullException("Odoo:Url");
            
            _logger.LogInformation("[ODOO] Client initialized for {Url} | DB: {Db} | User: {User}", _url, _db, _user);
        }

        public async Task<int> AuthenticateAsync()
        {
            if (_uid != -1) return _uid;

            var xml = $@"<?xml version=""1.0""?>
<methodCall>
<methodName>authenticate</methodName>
<params>
<param><value><string>{System.Security.SecurityElement.Escape(_db)}</string></value></param>
<param><value><string>{System.Security.SecurityElement.Escape(_user)}</string></value></param>
<param><value><string>{System.Security.SecurityElement.Escape(_apiKey)}</string></value></param>
<param><value><struct></struct></value></param>
</params>
</methodCall>";

            var response = await SendAsync("common", xml);
            _uid = ParseIntResponse(response);
            
            if (_uid <= 0)
            {
                _logger.LogWarning("[ODOO] Authentication failed for user {User} in DB {Db}. Odoo returned boolean false.", _user, _db);
            }
            else
            {
                _logger.LogInformation("[ODOO] Authentication successful. UID: {Uid}", _uid);
            }

            return _uid;
        }

        public async Task<List<T>> SearchAndReadAsync<T>(string model, object[][] domain, string[] fields) where T : new()
        {
            int uid = await AuthenticateAsync();
            if (uid <= 0) 
                throw new Exception($"Odoo Login failed for {_user}. Check credentials in Odoo Panel (API Keys).");
            
            var domainXml = BuildDomainXml(domain);
            var fieldsXml = BuildFieldsXml(fields);

            var xml = $@"<?xml version=""1.0""?>
<methodCall>
<methodName>execute_kw</methodName>
<params>
<param><value><string>{System.Security.SecurityElement.Escape(_db)}</string></value></param>
<param><value><int>{uid}</int></value></param>
<param><value><string>{System.Security.SecurityElement.Escape(_apiKey)}</string></value></param>
<param><value><string>{model}</string></value></param>
<param><value><string>search_read</string></value></param>
<param><value><array><data><value>{domainXml}</value></data></array></value></param>
<param>
<value>
<struct>
<member>
<name>fields</name>
<value>{fieldsXml}</value>
</member>
</struct>
</value>
</param>
</params>
</methodCall>";

            var response = await SendAsync("object", xml);
            return MapResponseToType<T>(response);
        }

        private List<T> MapResponseToType<T>(string xml) where T : new()
        {
            var results = new List<T>();
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            
            var fault = doc.SelectSingleNode("//fault");
            if (fault != null)
            {
                _logger.LogError("[ODOO] XML-RPC Fault: {Xml}", fault.OuterXml);
                return results;
            }

            var structs = doc.SelectNodes("//struct");
            if (structs == null) return results;

            foreach (XmlNode s in structs)
            {
                var item = new T();
                var props = typeof(T).GetProperties();
                
                foreach (XmlNode member in s.SelectNodes("member"))
                {
                    var name = member.SelectSingleNode("name")?.InnerText;
                    var valueNode = member.SelectSingleNode("value");
                    if (name == null || valueNode == null) continue;
                    
                    // Normalización de nombre: id -> Id, default_code -> DefaultCode/Default_Code
                    var prop = Array.Find(props, p => 
                        p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || 
                        p.Name.Equals(name.Replace("_", ""), StringComparison.OrdinalIgnoreCase));

                    if (prop != null)
                    {
                        object? finalValue = null;
                        var innerArray = valueNode.SelectSingleNode("array/data");
                        if (innerArray != null)
                        {
                            var firstValNode = innerArray.SelectSingleNode("value/*");
                            if (firstValNode != null) finalValue = firstValNode.InnerText;
                        }
                        else
                        {
                            var valNode = valueNode.SelectSingleNode("*");
                            finalValue = valNode?.InnerText ?? valueNode.InnerText;
                        }

                        if (finalValue != null)
                        {
                            try {
                                string valStr = finalValue.ToString() ?? "";
                                if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(Int32))
                                {
                                    if (int.TryParse(valStr, out int intVal))
                                        prop.SetValue(item, intVal);
                                }
                                else if (prop.PropertyType == typeof(double))
                                {
                                    if (double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
                                        prop.SetValue(item, dblVal);
                                }
                                else if (prop.PropertyType == typeof(bool))
                                {
                                    prop.SetValue(item, valStr == "1" || valStr.ToLower() == "true");
                                }
                                else
                                    prop.SetValue(item, Convert.ChangeType(valStr, prop.PropertyType));
                            } catch (Exception ex) {
                                _logger.LogDebug("Error mapping field {Name}: {Msg}", name, ex.Message);
                            }
                        }
                    }
                }
                results.Add(item);
            }
            return results;
        }

        private string BuildDomainXml(object[][] domain)
        {
            if (domain.Length == 0) return "<array><data/></array>";
            var sb = new StringBuilder("<array><data>");
            foreach (var criterion in domain)
            {
                sb.Append("<value><array><data>");
                foreach (var part in criterion)
                {
                    if (part is string s) sb.Append($"<value><string>{System.Security.SecurityElement.Escape(s)}</string></value>");
                    else if (part is bool b) sb.Append($"<value><boolean>{(b ? "1" : "0")}</boolean></value>");
                    else if (part is string[] arr) sb.Append(BuildFieldsXml(arr));
                    else sb.Append($"<value><int>{part}</int></value>");
                }
                sb.Append("</data></array></value>");
            }
            sb.Append("</data></array>");
            return sb.ToString();
        }

        private string BuildFieldsXml(string[] fields)
        {
            var sb = new StringBuilder("<array><data>");
            foreach (var field in fields) sb.Append($"<value><string>{field}</string></value>");
            sb.Append("</data></array>");
            return sb.ToString();
        }

        public async Task<bool> ExecuteAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null)
        {
            int uid = await AuthenticateAsync();
            return uid > 0;
        }

        private async Task<string> SendAsync(string service, string xml)
        {
            var content = new StringContent(xml, Encoding.UTF8, "text/xml");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");
            
            // Add custom User-Agent to avoid blocks on some instances
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "VictoriaWMS/1.0");

            var response = await _httpClient.PostAsync($"{_url}/xmlrpc/2/{service}", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private int ParseIntResponse(string xml)
        {
            try {
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var node = doc.SelectSingleNode("//int") ?? doc.SelectSingleNode("//i4");
                return node != null ? int.Parse(node.InnerText) : -1;
            } catch { return -1; }
        }
    }
}
