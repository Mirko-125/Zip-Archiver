using System.Net;
using System.Text;
using System.IO.Compression;

namespace ZipArchiver
{
    internal class Program
    {

        private static readonly object cachelock = new object();
        private static string root = "C:\\Users\\Mirko\\source\\repos\\Zip-Archiver\\Files";

        private static Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();

        static byte[] ZipFiles(string[] filenames)
        {
            Array.Sort(filenames, (x, y) => String.Compare(x, y));
            string filenamehash = String.Join(',', filenames);
            try
            {
                if (root == "") root = Environment.CurrentDirectory;

                lock (cachelock)
                {
                    byte[] res;
                    if (cache.TryGetValue(filenamehash, out res))
                    {
                        Console.WriteLine($"Zipped file found in cache: {filenamehash}");
                        return res;
                    }
                }
                byte[] zipbytes;
                using (MemoryStream mem = new MemoryStream())
                {
                    using (ZipArchive zip = new ZipArchive(mem, ZipArchiveMode.Create))
                    {
                        foreach (string f in filenames)
                        {
                            zip.CreateEntryFromFile(Path.Combine(root, f), f, CompressionLevel.Optimal);
                        }
                    }
                    zipbytes = mem.GetBuffer();
                }
                lock (cachelock)
                {
                    Console.WriteLine($"Zipped file added in cache: {filenamehash}");
                    cache[filenamehash] = zipbytes;
                }

                return zipbytes;

            }
            catch (System.Exception)
            {

                throw;
            }
        }
        static void SendResponse(HttpListenerContext c, byte[] body, string type = "text/plain; charset=utf-8", HttpStatusCode status = HttpStatusCode.OK)
        {
            HttpListenerResponse response = c.Response;
            response.ContentType = type;
            response.ContentLength64 = body.Length;
            response.StatusCode = (int)status;
            if(type == "application/zip") {
            response.AddHeader("Content-Disposition", $"attachment; filename=\"archived.zip\"");
            }
            System.IO.Stream output = response.OutputStream;
            output.Write(body, 0, body.Length);
            output.Close();
        }
        static void Accept(object c)
        {
            HttpListenerContext context = (HttpListenerContext)c;
            try
            {
                if (context.Request.HttpMethod != HttpMethod.Get.Method)
                {
                    SendResponse(context, Encoding.UTF8.GetBytes("Only GET allowed"), "text/plain; charset=utf-8", HttpStatusCode.BadRequest);
                    return;
                }
                var filenames = context.Request.Url.PathAndQuery.TrimStart('/').Split('&');
                filenames = filenames.Where(f => f != string.Empty).ToArray();
                //Console.WriteLine(filenames.Length);
                if (filenames.Length == 0)
                {
                    SendResponse(context, Encoding.UTF8.GetBytes("There's no files that are asked for"), "text/plain; charset=utf-8", HttpStatusCode.BadRequest);
                    return;
                }


                filenames = filenames.Where(f => File.Exists(Path.Combine(root, f))).ToArray();
                if (filenames.Length == 0)
                {
                    SendResponse(context, Encoding.UTF8.GetBytes("There are no such files with that name"), "text/plain; charset=utf-8", HttpStatusCode.NotFound);
                    return;
                }
                SendResponse(context, ZipFiles(filenames), "application/zip");
            }
            catch (System.Exception e)
            {
                SendResponse(context, Encoding.UTF8.GetBytes(e.Message), "text/plain; charset=utf-8", HttpStatusCode.InternalServerError);
            }
        }
        static void Main()
        {
            HttpListener hl = new HttpListener();
            hl.Prefixes.Add("http://127.0.0.1:8080/");
            hl.Start();
            Console.WriteLine("Server is on (http://127.0.0.1:8080/)");
            while (true)
            {
                // Thread thread = new Thread(new ParameterizedThreadStart(Accept));
                // thread.Start(hl.GetContext());
                // Iznad je impementacija sa obicnim tredovima dok je ispod sa tredpulom, obe rade, tredpul je optimalniji, pravi se tredpul pri svakoj sesiji
                var c = hl.GetContext();
                ThreadPool.QueueUserWorkItem(state => { Accept(c); });
            }
        }
    }
}