using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        public Category(int idIn, string nameIn)
        {
            Id = idIn;
            Name = nameIn;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            //setting up server
            var ip = IPAddress.Parse("127.0.0.1");
            var port = 5000;
            var server = new TcpListener(ip, port);
            var legalmethods = "create read update delete echo";
            var requirespath = "create read update delete";
            var requiresbody = "create update echo";
            var categories = new List<Category>();
            categories.Add(new Category(1, "Beverages"));
            categories.Add(new Category(2, "Condiments"));
            categories.Add(new Category(3, "Confections"));
            //var jsonformat = "} { \"method\": ,\"path\": ,\"date\": ,\"body\":";
            server.Start();
            
            //serving clients
            while (true)
            {
                Console.WriteLine("Waiting for client...");
                var client = server.AcceptTcpClient();
                ;
                var request = client.ReadRequest();
                Response response = new Response();

                if (request.Method == null && request.Date == null)
                    response.Status = "4 missing method, missing date";
                else if (!legalmethods.Contains(request.Method.ToLower()))
                    response.Status = "4 illegal method";
                else if (requirespath.Contains(request.Method.ToLower()) && request.Path == null)
                    response.Status = "4 missing resource";
                // how to check if unix format???
                else if (request.Date.Contains("/"))
                    response.Status = "4 illegal date";
                else if (requiresbody.Contains(request.Method.ToLower()) && request.Body == null)
                    response.Status = "4 missing body";
                //how to check if json format???
                else if (request.Method == "update" && request.Body == "Hello World")
                    response.Status = "4 illegal body";
                else if (request.Method == "echo")
                {
                    response.Status = "1 Ok";
                    response.Body = request.Body;
                }
                //how to check if the path is correct??
                else if (!request.Path.Contains("/categories"))
                    response.Status = "4 Bad Request";
                else if (request.Path.Contains("/categories"))
                {
                    int id = 0;
                    //debugging stuff Console.WriteLine(request.Path.Remove(0, request.Path.LastIndexOf("/")));
                    if (Int32.TryParse(request.Path.Remove(0, request.Path.LastIndexOf("/") + 1), out id))
                    {
                        foreach (var category in categories)
                        {
                            if (id == category.Id && request.Method == "create")
                            {
                                response.Status = "4 Bad Request";
                                goto IdFound;
                            }
                            if (id == category.Id && request.Method == "read")
                            {
                                response.Status = "1 Ok";
                                response.Body = category.ObjectToJson();
                                goto IdFound;
                            }
                            /*if (id == category.Id && request.Method == "update")
                            {
                                response.Status = "1 Ok";
                                response.Body = category.ObjectToJson();
                                goto IdFound;
                            }*/
                        }

                        response.Status = "5 Not Found";
                        IdFound: ;
                    }
                    else
                    {
                        if (request.Method == "read")
                        {
                            response.Status = "1 Ok";
                            response.Body = categories.ObjectToJson();
                        }
                        else
                        {
                            response.Status = "4 Bad Request";
                        }
                    }
                }

                if (request.Method == "Exit") break;
                
                Console.WriteLine($"Request: {request.ObjectToJson()}");
                Console.WriteLine($"Response: {response.ObjectToJson()}");
                client.SendResponse(response.ObjectToJson());
            }

            server.Stop();
        }
    }

    public static class Util
    {
        public static string ReadAllCategories(this List<Category> categories)
        {
            var allCategories = "[";
            foreach (var category in categories)
            {
                allCategories = allCategories + categories.ObjectToJson() + ",";
            }
            //remove the last comma and add "]"
            allCategories = allCategories.Remove(allCategories.Length - 1) + "]";
            return allCategories;
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
            //strm.ReadTimeout = 250;
            byte[] request = new byte[2048];
            using (var memStream = new MemoryStream())
            {
                int bytesread = 0;
                do
                {
                    bytesread = strm.Read(request, 0, request.Length);
                    memStream.Write(request, 0, bytesread);

                } while (bytesread == 2048);
                
                var requestData = Encoding.UTF8.GetString(memStream.ToArray());
                return JsonSerializer.Deserialize<Request>(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase});
            }
        }
    }
}
