using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security;

class OdooAuthTester
{
    static async Task Main()
    {
        var url = "https://perfectpty-qa-26815076.dev.odoo.com";
        var db = "perfectpty-qa-26815076";
        var user = "jortega@perfectpty.com";
        var apiKey = "34234139d36b24eeedc1ea4b418cec1ea5fbbd11";

        var xml = $@"<?xml version=""1.0""?>
<methodCall>
<methodName>authenticate</methodName>
<params>
<param><value><string>{SecurityElement.Escape(db)}</string></value></param>
<param><value><string>{SecurityElement.Escape(user)}</string></value></param>
<param><value><string>{SecurityElement.Escape(apiKey)}</string></value></param>
<param><value><struct></struct></value></param>
</params>
</methodCall>";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "VictoriaWMS-Diagnostic/1.0");
        
        var content = new StringContent(xml, Encoding.UTF8, "text/xml");
        var response = await client.PostAsync($"{url}/xmlrpc/2/common", content);
        var body = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Body: {body}");
    }
}
