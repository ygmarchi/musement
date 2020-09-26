using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Mail;
using System.IO;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;

public class Sitemap {
    string user;
    string password;
    string smtpServer;
    string path;
    int limit = -1;
    string[] recipients;
    string lang;
    bool lastmod;
    bool emailOnly;

    const string baseurl = "https://api.musement.com/api/v3/";
    const float cityPriority = 0.7F;
    const float activityPriority = 0.5F;

    async Task send () {
        SmtpClient smtp = new SmtpClient(smtpServer);
        smtp.EnableSsl = true;
        smtp.UseDefaultCredentials = false;
        smtp.Credentials = new System.Net.NetworkCredential (user, password);

        MailMessage mail = new MailMessage();
        mail.From = new MailAddress(user);
        foreach (string recipient in recipients) {
            mail.To.Add(recipient);
        }
        mail.Subject = "MUSEMENT.COM sitemap for " + lang;
        mail.Body = "Hello,\n\nplease find MUSEMENT.COM sitemap as attachment.";

        if (!emailOnly)
            await create ();

        Attachment attachment = new Attachment(path);
        mail.Attachments.Add(attachment);
        smtp.Send(mail);
        Console.WriteLine ("Sitemap sent");
    }

    async Task create () {
        
        String url = baseurl + "cities";
        if (limit > 0) 
            url += "?limit=" + limit;
        
        Console.WriteLine ("Querying cities, url " + url);
        var client = new HttpClient();
        client.BaseAddress = new Uri(url);
        client.DefaultRequestHeaders.Add("Accept-Language", lang);
        var httpResponse = await client.GetAsync(url);
        httpResponse.EnsureSuccessStatusCode();
        var stringResponse = await httpResponse.Content.ReadAsStringAsync();

        var cities = JObject.Parse ("{ cities :" + stringResponse + "}");
        
        var stream = File.Create (path);
        try {
            var options = new XmlWriterSettings ();
            options.Indent = true;
            options.Encoding = System.Text.Encoding.UTF8;
            var xml = XmlWriter.Create (stream, options);
            
            xml.WriteStartDocument ();
            xml.WriteStartElement ( "urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                        
            foreach (JObject city in cities ["cities"]) {
                await write (city, xml);
            }    
            
            xml.WriteEndElement (); 
            xml.WriteEndDocument ();

            xml.Close ();

            Console.WriteLine ("Sitemap written to " + path);
        } finally {
            
            stream.Close ();
        }
    }       

    async Task writeLastmod (string url, XmlWriter xml) {
        if (lastmod) {
            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("Accept-Language", lang);
            var httpResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            httpResponse.EnsureSuccessStatusCode();
            var lastModified = httpResponse.Content.Headers.LastModified;
            if (lastModified.HasValue) {
                xml.WriteStartElement ("lastmod");
                xml.WriteString (lastModified.Value.ToString ("u"));
                xml.WriteEndElement();        
            }
        }
    }

    async Task write (JObject city, XmlWriter xml) {
        xml.WriteStartElement ("url");
    
        var cityURL = (string) city ["url"];
        xml.WriteStartElement ("loc");
        xml.WriteString (cityURL);
        xml.WriteEndElement ();
    
        await writeLastmod (cityURL, xml);

        xml.WriteStartElement ("priority");
        xml.WriteString (cityPriority.ToString ());
        xml.WriteEndElement ();
    
        xml.WriteEndElement (); 
    
        var url = baseurl + "cities/" + (string) city ["id"] + "/activities";
        if (limit > 0) 
            url += "?limit=" + limit;
        Console.WriteLine ("Querying activities for city " + ((string) city ["name"])  + ", url " + url);
        var client = new HttpClient();
        client.BaseAddress = new Uri(url);
        client.DefaultRequestHeaders.Add("Accept-Language", lang);
        var httpResponse = await client.GetAsync(url);
        httpResponse.EnsureSuccessStatusCode();
        var stringResponse = await httpResponse.Content.ReadAsStringAsync();
        
        var activities = JObject.Parse (stringResponse);
        foreach (JObject activity in activities ["data"]) {
            xml.WriteStartElement ("url");
    
            var activityURL = (string) activity ["url"];
            xml.WriteStartElement ("loc");
            xml.WriteString (activityURL);
            xml.WriteEndElement ();

            await writeLastmod (activityURL, xml);

            xml.WriteStartElement ("priority");
            xml.WriteString (activityPriority.ToString ());
            xml.WriteEndElement ();
        
            xml.WriteEndElement ();
        }
    }



    static Sitemap build (string [] args) {
        Sitemap program = new Sitemap ();
        ArrayList recipients = new ArrayList ();
        for (var i = 0; i < args.Length; i++) {
            var arg = args [i];
            switch (arg) {
                case "-u":
                case "--user":
                    program.user = args [++i];
                    break;
                case "-p":
                case "--password":
                    program.password = args [++i];
                    break;
                case "-s":
                case "--server":
                    program.smtpServer = args [++i];
                    break;
                case "-n":
                case "--limit":
                    program.limit = Int16.Parse (args [++i]);
                    break;
                case "-l":
                case "--lang":
                    program.lang = args [++i];
                    break;
                case "-r":
                case "--recipient":
                    recipients.Add (args [++i]);
                    break;
                case "-d":
                case "--lastModified":
                    program.lastmod = true;
                    break;
                case "-e":
                case "--nogen":
                    program.emailOnly = true;
                    break;
                case "-h":
                case "--help":
                    throw new ArgumentException ("help");
                default:
                    if (program.path != null) 
                        throw new ArgumentException ("Multiple output file");
                    program.path = arg;
                    break;
            }
        }
        program.recipients = (string []) recipients.ToArray (typeof (string ));

        if (program.path == null) {
            program.path = "sitemap.xml";
        }

        if (program.lang == null) {
            program.lang = "it-iT";
        }

        if (program.limit < 0) {
            program.limit = 20;
        }

        if (program.emailOnly) {
            if (!File.Exists (program.path))
                throw new ArgumentException (program.path + " does not exists");
            
            if (program.recipients.Length == 0)
                throw new ArgumentException ("email implied but no recipient provided");
        } 

        if (program.recipients.Length > 0) {
            if (program.user == null)
                throw new ArgumentException ("Mandatory user");
            if (program.password == null)
                throw new ArgumentException ("Mandatory password");
            if (program.smtpServer == null)
                throw new ArgumentException ("Mandatory smptServer");
        }
        return program;
    }

    static async Task Main(string[] args) {
        try {
            Sitemap program = build (args);
            if (program.recipients.Length == 0) {
                await program.create ();
            } else {
                await program.send ();
            }
        } catch (ArgumentException e) {
            if (e.Message != "help")
                Console.Error.WriteLine (e.Message);
            usage ();
        }
    }    
    static void usage () {
        Console.Out.Write (@"
Usage:

sitemap <options> <file>

<file> is optional, defaults to sitemap.xml inside current directory
<options> are
    -l:
    --lang:
        sitemap language, defaults to it-IT
    -u:
    --user:
        email account (mandatory if recipients specified)
    -p:
    --password:
        email password (mandatory if recipients specified)
    -s:
    --server:
        email smpt server (mandatory if recipients specified)
    -n:
    --limit:
        limit of cities and activities in sitemap, defaults to 20, 0 means no limit, negative means default
    -r:
    --recipient:
        email recipient, if not provided non email will be sent, can be repeated
    -d
    --lastModified
        query each page to include lastModified information in the sitemap, defaults to false
    -e
    --nogen:
        don't generate sitemap, just send it if was already generated
    -h 
    --help
        prints this usage

"
        );
    }
}
