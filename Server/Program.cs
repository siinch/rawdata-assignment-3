using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Server
{
    public class Request
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Date { get; set; }
        public string Body { get; set; }
    }
    
    public class Response
    {
        public string Status { get; set; }
        public string Body { get; set; }
    }
    
    public class Category
    {
        [JsonPropertyName("cid")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /*public Category(int idIn, string nameIn)
        {
            Id = idIn;
            Name = nameIn;
        }*/
    }

    class Program
    {
        
        public static string legalmethods = "create read update delete echo";
        public static string requirespath = "create read update delete";
        public static string requiresbody = "create update echo";
        public static List<object> categories = new List<object>();
        static void Main(string[] args)
        {
            //setting up server
            var ip = IPAddress.Parse("127.0.0.1");
            var port = 5000;
            var server = new TcpListener(ip, port);
            categories.Add(new {cid=1, name="Beverages"});
            categories.Add(new {cid=2, name="Condiments"});
            categories.Add(new {cid=3, name="Confections"});
            //var jsonformat = "} { \"method\": ,\"path\": ,\"date\": ,\"body\":";
            server.Start();
            
            //serving clients
            while (true)
            {
                Console.WriteLine("Waiting for client...");
                var client = server.AcceptTcpClient();
                new Thread(() => HandleClientRequest(client)).Start();
            }

            server.Stop();
        }

        public static void HandleClientRequest(TcpClient client)
        {
            Console.WriteLine("Waiting for request...");
                var request = client.ReadRequest();
                Console.WriteLine($"Request: {request.ObjectToJson()}");
                Response response = new Response();

                DateTime testDate;

                if (request.Method == null && request.Date == null)
                    response.Status = "4 missing method, missing date";
                else if (!legalmethods.Contains(request.Method.ToLower()))
                    response.Status = "4 illegal method";
                else if (requirespath.Contains(request.Method.ToLower()) && request.Path == null)
                    response.Status = "4 missing resource";
                // how to check if unix format???
                else if (DateTime.TryParse(request.Date, out testDate))
                    response.Status = "4 illegal date";
                else if (requiresbody.Contains(request.Method.ToLower()) && request.Body == null)
                    response.Status = "4 missing body";
                //how to check if json format???
                else if (request.Method == "update" && !request.Body.IsJsonFormat())
                    response.Status = "4 illegal body";
                else if (request.Method == "echo")
                {
                    response.Status = "1 Ok";
                    response.Body = request.Body;
                }
                //how to check if the path is correct??
                else if (!request.Path.Contains("/categories"))
                {
                    response.Status = "4 Bad Request";
                    //response.Body = null;
                }
                else if (request.Path.Contains("/categories"))
                {
                    int id = 0;
                    //debugging stuff Console.WriteLine(request.Path.Remove(0, request.Path.LastIndexOf("/")));
                    if (Int32.TryParse(request.Path.Remove(0, request.Path.LastIndexOf("/") + 1), out id))
                    {
                        foreach (var category in categories)
                        {
                            //Need explicit cat to Category because it is generic 
                            var genericToCategory = category.ObjectToJson().JsonToObject<Category>();
                            if (id == genericToCategory.Id && request.Method == "create")
                            {
                                response.Status = "4 Bad Request";
                                goto IdFound;
                            }
                            if (id == genericToCategory.Id && request.Method == "read")
                            {
                                response.Status = "1 Ok";
                                response.Body = category.ObjectToJson();
                                goto IdFound;
                            }
                            if (id == genericToCategory.Id && request.Method == "update")
                            {
                                response.Status = "3 Updated";
                                var tempCat = request.Body.JsonToObject<Category>();
                                categories.Insert(categories.IndexOf(category), tempCat);
                                categories.Remove(category);
                                goto IdFound;
                            }
                            if (id == genericToCategory.Id && request.Method == "delete")
                            {
                                response.Status = "1 Ok";
                                categories.Remove(category);
                                goto IdFound;
                            }
                        }

                        response.Status = "5 Not Found";
                        IdFound: ;
                    }
                    else if (request.Path.Remove(0, request.Path.LastIndexOf("/") + 1) == "categories")
                    {
                        if (request.Method == "read")
                        {
                            response.Status = "1 Ok";
                            response.Body = categories.ObjectToJson();
                        }

                        else if (request.Method == "create")
                        {
                            response.Status = "2 Created";
                            var existingIds = new List<int>();
                            foreach (var category in categories)
                            {
                                existingIds.Add(category.ObjectToJson().JsonToObject<Category>().Id);
                            }

                            var newId = 1;
                            while (existingIds.Contains(newId))
                                newId++;

                            var newName = request.Body.JsonToObject<Category>().Name;
                            categories.Add(new{cid=newId, name=newName});
                            response.Body = new {cid = newId, name = newName}.ObjectToJson();
                        }
                        else if (request.Method == "delete" || request.Method =="update")
                        {
                            response.Status = "4 Bad Request";
                            response.Body = null;
                        }
                    }
                    else
                    {
                        response.Status = "4 Bad Request";
                        response.Body = null;
                    }
                }

            Console.WriteLine($"Response: {response.ObjectToJson()}");
            Console.WriteLine("");
            client.SendResponse(response.ObjectToJson());
        }
    }

    public static class Util
    {
        public static bool IsJsonFormat(this string s)
        {
            try
            {
                s.JsonToObject<Category>();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("bad json");
                return false;
            }
        }
        public static string ObjectToJson(this object data)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public static T JsonToObject<T>(this string element)
        {
            return JsonSerializer.Deserialize<T>(element, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public static void SendResponse(this TcpClient client, string response)
        {
            var msg = Encoding.UTF8.GetBytes(response);
            client.GetStream().Write(msg, 0, msg.Length);
        }

        public static Request ReadRequest(this TcpClient client)
        {
            var strm = client.GetStream();
            Console.WriteLine("Got stream!");
            //strm.ReadTimeout = 5000;
            byte[] request = new byte[2048];
            using (var memStream = new MemoryStream())
            {
                int bytesread = 0;
                do
                {
                    Console.WriteLine("Reading stream...");
                    bytesread = strm.Read(request, 0, request.Length);
                    Console.WriteLine("Read stream!");
                    memStream.Write(request, 0, bytesread);

                } while (bytesread == 2048);

                var requestData = Encoding.UTF8.GetString(memStream.ToArray());
                try
                {
                    return JsonSerializer.Deserialize<Request>(requestData, new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase});
                }
                catch (Exception e)
                {
                    Console.WriteLine("Some exception in ReadRequest. Continue....");
                    return new Request();
                }
            }
        }
    }
}
