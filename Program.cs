using System;
using System.Drawing;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace PrimaryScreenTransmitterOverHTTP
{
    //Primary screen transmitter over http
    //The application allows viewing the device primary screen in web browser
    //Application system requirements: OS Windows whith .NET Framework 3.5
    class Program
    {
        private static readonly string ServiceName = "PrimaryScreenTransmitterOverHTTP";
        private static readonly string title = "Primary screen transmitter over HTTP";
        private static readonly string copyright = "Copyright © 2021 Dmitry Tsyganov (https://github.com/tsyganovd-npo/PrimaryScreenTransmitterOverHTTP) The MIT License.";
        private static Int16 port = 5800;
        private static Int16 refresh = 10;
        private static string msg;
        private static readonly Encoding charset = Encoding.UTF8;
        private static readonly string NL = Environment.NewLine;
        private static Thread httpserver;
        private static bool isRuning = true;
        static void Main(string[] args)
        {
            if (args.Length > 0)
                Int16.TryParse(args[0], out port);
            if (args.Length > 1)
                Int16.TryParse(args[1], out refresh);
            msg += title + ". " + copyright;
            httpserver = new Thread(new ThreadStart(HTTPServer));
            httpserver.Start();
            while (isRuning)
            {
                System.Threading.Thread.Sleep(10000);
            }
        }
        private static void HTTPServer()
        {
            HttpListener httplistener = new HttpListener();
            //httplistener.Prefixes.Add($"http://localhost:{port}/");
            //httplistener.Prefixes.Add($"http://127.0.0.1:{port}/");
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    httplistener.Prefixes.Add($"http://{addr}:{port}/");
                }
            }
            foreach (string prefix in httplistener.Prefixes)
            {
                Program.msg += Environment.NewLine + $"Add HTTP Server prefix: {prefix}";
            }
            try
            {
                httplistener.Start();
                Program.msg += Environment.NewLine + $"HTTP server started on port {port}";
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    eventLog.Source = ServiceName;
                    eventLog.WriteEntry(Program.msg, System.Diagnostics.EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                {
                    eventLog.Source = ServiceName;
                    eventLog.WriteEntry($"Stopping HTTP server with Exception - {ex.Message}.", System.Diagnostics.EventLogEntryType.Warning);
                }
                Program.isRuning = false;
                return;
            }
            while (true)
            {
                try
                {
                    GC.Collect();
                    HttpListenerContext httpListenerContext = httplistener.GetContext();
                    ThreadPool.QueueUserWorkItem(ContextThread, httpListenerContext);
                }
                catch (Exception ex)
                {
                    httplistener.Stop();
                    using (System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog("Application"))
                    {
                        eventLog.Source = ServiceName;
                        eventLog.WriteEntry($"Stopping HTTP server with Exception - {ex.Message}.", System.Diagnostics.EventLogEntryType.Warning);
                    }
                    Program.isRuning = false;
                    return;
                }
            }
        }
        private static void ContextThread(object state)
        {
            try
            {
                HttpListenerContext httpListenerContext = (HttpListenerContext)state;
                string path = httpListenerContext.Request.Url.AbsolutePath.ToLower();
                if (path == "/")
                {
                    string style = @"
                    <style>html{
                           background: url(" + DateTime.Now.ToString("dd-MM-yyyy-HH-mm") + @".jpg) no-repeat center center fixed;
                           -webkit-background-size: cover;
                           -moz-background-size: cover;
                           -o-background-size: cover;
                           background-size: cover;
                    }</style>";

                    string head = $"<!DOCTYPE HTML>{NL}<html dir='ltr' lang='EN'>{NL}" +
                        $"<head>{NL}\t<meta http-equiv='Content-Type' content='text/html; charset={charset.WebName}'>{NL}" +
                        $"\t<title>{title}</title>{NL}{style}{NL}<meta http-equiv='refresh' content='{refresh}'>{NL}</head>{NL}";

                    byte[] buffer = charset.GetBytes($"{head}" +
                        $"<body></body>{NL}</html>");
                    httpListenerContext.Response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = httpListenerContext.Response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                    return;
                }
                if (path.ToLower() == "/exit")
                {
                    byte[] buffer = charset.GetBytes($"<body>{title} shutdown.</body>{NL}</html>");
                    httpListenerContext.Response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = httpListenerContext.Response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                    Program.isRuning = false;
                    httpserver.Abort();
                    return;
                }
                if (path.EndsWith("/")) path = path.Remove(path.Length - 1, 1);
                if (System.IO.Path.HasExtension(path))
                {

                    switch (System.IO.Path.GetExtension(path))
                    {
                        case ".jpeg":
                        case ".jpg":
                            SendImage(httpListenerContext.Response, System.IO.Path.GetExtension(path).Substring(1));
                            return;
                    }
                }
                httpListenerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                httpListenerContext.Response.SendChunked = false;
                byte[] errbuffer = charset.GetBytes($"BadRequest");
                httpListenerContext.Response.ContentLength64 = errbuffer.Length;
                System.IO.Stream erroutput = httpListenerContext.Response.OutputStream;
                erroutput.Write(errbuffer, 0, errbuffer.Length);
                erroutput.Close();
            }
            catch { }
        }
        private static void SendImage(HttpListenerResponse httpListenerResponse, string extantion)
        {
            Bitmap primaryscreen = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Graphics graphics = Graphics.FromImage(primaryscreen as Image);
            graphics.CopyFromScreen(0, 0, 0, 0, primaryscreen.Size);
            httpListenerResponse.ContentType = "image/" + extantion;
            System.Drawing.Image img = (System.Drawing.Image)primaryscreen;
            System.Drawing.ImageConverter converter = new System.Drawing.ImageConverter();
            byte[] buffer = (byte[])converter.ConvertTo(img, typeof(byte[]));
            httpListenerResponse.ContentLength64 = buffer.Length;
            System.IO.Stream output = httpListenerResponse.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
    }
}
