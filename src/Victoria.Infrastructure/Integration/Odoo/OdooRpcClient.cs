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
        Task<object[]> SearchAndReadAsync(string model, object[][] domain, string[] fields);
        Task<bool> ExecuteAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null);
    }

    public class OdooRpcClient : IOdooRpcClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _url;
        private readonly string _db;
        private readonly string _user;
        private readonly string _apiKey;
        private int _uid = -1;

        public OdooRpcClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _url = config["Odoo:Url"] ?? throw new ArgumentNullException("Odoo:Url");
            _db = config["Odoo:Db"] ?? throw new ArgumentNullException("Odoo:Db");
            _user = config["Odoo:User"] ?? throw new ArgumentNullException("Odoo:User");
            _apiKey = config["Odoo:ApiKey"] ?? throw new ArgumentNullException("Odoo:ApiKey");
        }

        public async Task<int> AuthenticateAsync()
        {
            if (_uid != -1) return _uid;

            var xml = $@"<?xml version='1.0'?>
            <methodCall>
                <methodName>authenticate</methodName>
                <params>
                    <param><value><string>{_db}</string></value></param>
                    <param><value><string>{_user}</string></value></param>
                    <param><value><string>{_apiKey}</string></value></param>
                    <param><value><struct /></value></param>
                </params>
            </methodCall>";

            var response = await SendAsync("common", xml);
            _uid = ParseIntResponse(response);
            return _uid;
        }

        public async Task<object[]> SearchAndReadAsync(string model, object[][] domain, string[] fields)
        {
            int uid = await AuthenticateAsync();
            // Implementación simplificada de XML-RPC call para search_read
            // En una implementación real, se construiría el XML dinámicamente para los parámetros.
            // Para fines del demo, mostramos la estructura.
            
            Console.WriteLine($"[RPC] Searching model {model} for UID {uid}");
            return Array.Empty<object>(); // Simulación de respuesta
        }

        public async Task<bool> ExecuteAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null)
        {
            int uid = await AuthenticateAsync();
            Console.WriteLine($"[RPC] Executing {method} on {model} with IDs {string.Join(",", ids)}");
            return true;
        }

        private async Task<string> SendAsync(string service, string xml)
        {
            var content = new StringContent(xml, Encoding.UTF8, "text/xml");
            var response = await _httpClient.PostAsync($"{_url}/xmlrpc/2/{service}", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private int ParseIntResponse(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var node = doc.SelectSingleNode("//int") ?? doc.SelectSingleNode("//i4");
            return node != null ? int.Parse(node.InnerText) : -1;
        }
    }
}
