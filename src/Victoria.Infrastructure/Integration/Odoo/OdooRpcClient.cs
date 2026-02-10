using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Victoria.Core.Interfaces;

namespace Victoria.Infrastructure.Integration.Odoo
{
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
            _url = (config["Odoo:Url"] ?? "").Trim().TrimEnd('/');
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

        public async Task<List<T>> SearchAndReadAsync<T>(string model, object[][] domain, string[] fields, int limit = 0, int offset = 0) where T : new()
        {
            var kwargs = new Dictionary<string, object> { { "fields", fields } };
            if (limit > 0) kwargs["limit"] = limit;
            if (offset > 0) kwargs["offset"] = offset;

            object? response = null;
            int retries = 3;
            while (retries > 0)
            {
                try 
                {
                    response = await ExecuteActionAsync(model, "search_read", new object[] { domain }, kwargs);
                    break;
                }
                catch (Exception ex) when (retries > 1)
                {
                    _logger.LogWarning("[ODOO-RPC] SearchAndRead failed, retrying... ({Retries} left). Error: {Msg}", retries - 1, ex.Message);
                    await Task.Delay(2000);
                    retries--;
                }
                catch (Exception)
                {
                    throw;
                }
            }

            if (response == null) return new List<T>();

            if (response is System.Collections.IEnumerable list)
            {
                var results = new List<T>();
                var json = System.Text.Json.JsonSerializer.Serialize(response);
                var options = new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };
                
                try 
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<T>>(json, options) ?? new List<T>();
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning("[ODOO-RPC] JSON Deserialization failed: {Msg}. Falling back to manual mapping.", ex.Message);
                    // Fallback to manual mapping if JSON fails
                    foreach (var item in list)
                    {
                        if (item is Dictionary<string, object?> dict)
                        {
                            var obj = new T();
                            var props = typeof(T).GetProperties();
                            foreach (var prop in props)
                            {
                                // Handle JsonPropertyName attribute
                                var attr = (System.Text.Json.Serialization.JsonPropertyNameAttribute?)Attribute.GetCustomAttribute(prop, typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute));
                                string key = attr?.Name ?? prop.Name;
                                
                                // Try finding by key, case-insensitive or underscore-insensitive
                                string? foundKey = null;
                                if (dict.ContainsKey(key)) foundKey = key;
                                else foundKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase) || k.Replace("_", "").Equals(key.Replace("_", ""), StringComparison.OrdinalIgnoreCase));

                                if (foundKey != null && dict.TryGetValue(foundKey, out var val))
                                {
                                    if (val == null || (val is bool b && !b && prop.PropertyType != typeof(bool)))
                                    {
                                        // Skip or set to null/default
                                        continue;
                                    }

                                    try 
                                    {
                                        if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(Int32))
                                            prop.SetValue(obj, Convert.ToInt32(val));
                                        else if (prop.PropertyType == typeof(double))
                                            prop.SetValue(obj, Convert.ToDouble(val, System.Globalization.CultureInfo.InvariantCulture));
                                        else if (prop.PropertyType == typeof(string))
                                            prop.SetValue(obj, val?.ToString());
                                        else if (prop.PropertyType == typeof(bool))
                                            prop.SetValue(obj, (val is bool bv && bv) || val?.ToString() == "1" || val?.ToString()?.ToLower() == "true");
                                        else
                                            prop.SetValue(obj, val);
                                    }
                                    catch { /* skip error */ }
                                }
                            }
                            results.Add(obj);
                        }
                    }
                    return results;
                }
            }

            return new List<T>();
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
                        var innerValueNode = valueNode.SelectSingleNode("*");
                        object? finalValue = ParseValueNode(innerValueNode);

                        if (finalValue != null)
                        {
                            try 
                            {
                                // If target is object, just set it (will preserve Lists if it was an array)
                                if (prop.PropertyType == typeof(object))
                                {
                                    prop.SetValue(item, finalValue);
                                }
                                else
                                {
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
                                        prop.SetValue(item, valStr == "1" || valStr.ToLower() == "true" || (finalValue is bool b && b));
                                    }
                                    else
                                        prop.SetValue(item, Convert.ChangeType(valStr, prop.PropertyType));
                                }
                            } 
                            catch (Exception ex) 
                            {
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
                    else if (part is bool[] bArr) 
                    {
                        sb.Append("<value><array><data>");
                        foreach(var v in bArr) sb.Append($"<value><boolean>{(v ? "1" : "0")}</boolean></value>");
                        sb.Append("</data></array></value>");
                    }
                    else if (part is object[] oArr)
                    {
                        sb.Append("<value><array><data>");
                        foreach(var v in oArr) 
                        {
                            if (v is string strVal) sb.Append($"<value><string>{System.Security.SecurityElement.Escape(strVal)}</string></value>");
                            else if (v is bool bv) sb.Append($"<value><boolean>{(bv ? "1" : "0")}</boolean></value>");
                            else sb.Append($"<value><int>{v}</int></value>");
                        }
                        sb.Append("</data></array></value>");
                    }
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
            if (uid <= 0) return false;

            var xml = BuildExecuteKwXml(model, method, ids, values);
            var response = await SendAsync("object", xml);
            
            // Si retorna un fault, es false
            if (response.Contains("<fault>")) return false;
            
            // Verificar si el valor es <boolean>1</boolean> o un <int> > 0
            return response.Contains("<boolean>1</boolean>") || ParseIntResponse(response) > 0;
        }

        public async Task<object?> ExecuteActionAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null)
        {
            int uid = await AuthenticateAsync();
            if (uid <= 0) return null;

            var xml = BuildExecuteKwXml(model, method, ids, values);
            var response = await SendAsync("object", xml);

            var doc = new XmlDocument();
            doc.LoadXml(response);

            var fault = doc.SelectSingleNode("//fault");
            if (fault != null) return null;

            var valueNode = doc.SelectSingleNode("//value/*");
            var result = ParseValueNode(valueNode);
            _logger.LogInformation("[ODOO-DEBUG] ExecuteAction {Method} Result: {ResultType}", method, result?.GetType().Name ?? "null");
            return result;
        }

        private string BuildExecuteKwXml(string model, string method, object[] ids, Dictionary<string, object>? values = null)
        {
            var idsXml = new StringBuilder("<array><data>");
            foreach (var id in ids) 
            {
                idsXml.Append("<value>");
                idsXml.Append(BuildValueXml(id));
                idsXml.Append("</value>");
            }
            idsXml.Append("</data></array>");

            var kwargsXml = "";
            if (values != null && values.Count > 0)
            {
                kwargsXml = BuildStructXml(values);
            }

            return $@"<?xml version=""1.0""?>
<methodCall>
<methodName>execute_kw</methodName>
<params>
<param><value><string>{System.Security.SecurityElement.Escape(_db)}</string></value></param>
<param><value><int>{_uid}</int></value></param>
<param><value><string>{System.Security.SecurityElement.Escape(_apiKey)}</string></value></param>
<param><value><string>{model}</string></value></param>
<param><value><string>{method}</string></value></param>
<param><value>{idsXml}</value></param>
{(string.IsNullOrEmpty(kwargsXml) ? "" : $"<param><value>{kwargsXml}</value></param>")}
</params>
</methodCall>";
        }

        private string BuildStructXml(Dictionary<string, object> values)
        {
            var sb = new StringBuilder("<struct>");
            foreach (var kv in values)
            {
                sb.Append("<member>");
                sb.Append($"<name>{kv.Key}</name>");
                sb.Append("<value>");
                sb.Append(BuildValueXml(kv.Value));
                sb.Append("</value>");
                sb.Append("</member>");
            }
            sb.Append("</struct>");
            return sb.ToString();
        }

        private string BuildValueXml(object? value)
        {
            if (value == null) return "<nil/>";
            if (value is string s) return $"<string>{System.Security.SecurityElement.Escape(s)}</string>";
            if (value is bool b) return $"<boolean>{(b ? "1" : "0")}</boolean>";
            if (value is int i) return $"<int>{i}</int>";
            if (value is double d) return $"<double>{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}</double>";
            if (value is long l) return $"<int>{l}</int>";
            if (value is Dictionary<string, object> dict) return BuildStructXml(dict);
            if (value is System.Collections.IEnumerable list && !(value is string))
            {
                var sb = new StringBuilder("<array><data>");
                foreach (var item in list)
                {
                    sb.Append("<value>");
                    sb.Append(BuildValueXml(item));
                    sb.Append("</value>");
                }
                sb.Append("</data></array>");
                return sb.ToString();
            }
            return $"<string>{System.Security.SecurityElement.Escape(value.ToString() ?? "")}</string>";
        }

        private object? ParseValueNode(XmlNode? node)
        {
            if (node == null) return null;
            
            switch (node.Name)
            {
                case "string": return node.InnerText;
                case "int": 
                case "i4": return int.Parse(node.InnerText);
                case "boolean": return node.InnerText == "1";
                case "double": return double.Parse(node.InnerText);
                case "struct":
                    var dict = new Dictionary<string, object?>();
                    foreach (XmlNode member in node.SelectNodes("member"))
                    {
                        var name = member.SelectSingleNode("name")?.InnerText;
                        var val = member.SelectSingleNode("value/*");
                        if (name != null) dict[name] = ParseValueNode(val);
                    }
                    return dict;
                case "array":
                    var list = new List<object?>();
                    foreach (XmlNode val in node.SelectNodes("data/value/*"))
                    {
                        list.Add(ParseValueNode(val));
                    }
                    return list;
                default: return node.InnerText;
            }
        }

        public async Task<T> ExecuteKwAsync<T>(string model, string method, object[] args, Dictionary<string, object>? kwargs = null)
        {
            var uid = await AuthenticateAsync();
            if (uid <= 0) throw new Exception("Odoo Authentication failed");

            // Build args array XML
            var argsSb = new StringBuilder();
            if (args != null)
            {
                foreach (var arg in args)
                {
                    argsSb.Append("<value>");
                    argsSb.Append(BuildValueXml(arg));
                    argsSb.Append("</value>");
                }
            }

            // Build kwargs struct XML
            var kwargsXml = "";
            if (kwargs != null)
            {
                kwargsXml = BuildStructXml(kwargs);
            }
            else
            {
                kwargsXml = "<struct></struct>";
            }

            var xml = $@"<?xml version=""1.0""?>
<methodCall>
<methodName>execute_kw</methodName>
<params>
<param><value><string>{System.Security.SecurityElement.Escape(_db)}</string></value></param>
<param><value><int>{uid}</int></value></param>
<param><value><string>{System.Security.SecurityElement.Escape(_apiKey)}</string></value></param>
<param><value><string>{model}</string></value></param>
<param><value><string>{method}</string></value></param>
<param><value><array><data>{argsSb}</data></array></value></param>
<param><value>{kwargsXml}</value></param>
</params>
</methodCall>";

            var response = await SendAsync("object", xml);
            
            // Parse response using existing helpers
            // Note: ParseResponse is generic in some implementations, but here we might need to adapt.
            // SearchAndReadAsync uses ParseResponse<List<T>>
            // We need a generic parser.
            
            var doc = new XmlDocument();
            doc.LoadXml(response);
            var fault = doc.SelectSingleNode("//fault");
            if (fault != null)
            {
                throw new Exception($"Odoo RPC Fault: {fault.OuterXml}");
            }

            var paramValue = doc.SelectSingleNode("//methodResponse/params/param/value/*");
            if (paramValue == null) return default;

            // This is a simplified parser mapping. 
            // Since T is generic, we can rely on Newtonsoft or manual mapping if T is Dictionary.
            var val = ParseValueNode(paramValue);
            
            if (val is T tVal) return tVal;
            
            // Try casting via JSON if direct cast fails (e.g. object to Dictionary)
            try 
            {
                var json = System.Text.Json.JsonSerializer.Serialize(val);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }

        private async Task<string> SendAsync(string service, string xml)
        {
            var content = new StringContent(xml, Encoding.UTF8, "text/xml");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");
            
            // Add custom User-Agent to avoid blocks on some instances
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "VictoriaWMS/1.0");

            var requestUrl = $"{_url}/xmlrpc/2/{service}";
            _logger.LogInformation("[ODOO] POST {RequestUrl}", requestUrl);
            var response = await _httpClient.PostAsync(requestUrl, content);
            try 
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Odoo Request Failed: {response.StatusCode} at {requestUrl}. Body: {responseBody}", ex);
            }
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
