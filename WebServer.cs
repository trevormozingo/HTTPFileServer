using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;

namespace CS422
{
	public class WebServer
	{
		private const int request_line_size = 2048;
		private const int header_size = 100 * 1024;

		public static bool server_active = true;
		private static ConcurrentBag<WebService> services = new ConcurrentBag<WebService>();
		private static ThreadLake thread_lake;
		private static Thread listener;

		public static bool Start(int port, int num_threads)
		{
			if (num_threads <= 0) { num_threads = 64; }
				
			thread_lake = new ThreadLake(num_threads);

			ThreadStart thread_start = delegate {
				listen(port);
			};

			listener = new Thread(thread_start);

			listener.Start();

			return true;
		}

		public static void Stop()
		{
			server_active = false;

RESTART:
			foreach (Thread thread in thread_lake.Threads)
			{
				if (thread.IsAlive)
				{
					if (thread_lake.Count <= 0)
					{
						thread_lake.Insert(null);
						goto RESTART;
					}
				}
			}

			listener.Abort();
			thread_lake.Dispose();
		}
			
		private static void listen(int port)
		{
			TcpListener tcpListener = new TcpListener(IPAddress.Any, port);
			tcpListener.Start();

			while (server_active)
			{
				TcpClient tcp_client = tcpListener.AcceptTcpClient();
				thread_lake.Insert(tcp_client);
			}
		}

		public static ConcurrentBag<WebService>Services
		{
			get { return services; }
		}

		public static void AddService(WebService service)
		{
			services.Add(service);
		}
			
		public static WebRequest BuildRequest(TcpClient client)
		{
			byte[] buffer = new byte[128];
			byte[] client_data = new byte[0];

			int num_bytes;
			int status = -1;

			NetworkStream stream = client.GetStream();
			stream.ReadTimeout = 1000;

			try 
			{

				DateTime start = DateTime.Now;

				while((num_bytes = stream.Read(buffer, 0, buffer.Length)) != 0) 
				{

					if (10 <= (DateTime.Now - start).TotalSeconds) { throw new IOException(); }

					byte[] expandArray = new byte[client_data.Length + num_bytes];
					Array.Copy(client_data, 0, expandArray, 0, client_data.Length);
					Array.Copy(buffer, 0, expandArray, client_data.Length, num_bytes);
					client_data = expandArray;

					if (status != 2) 
					{ 
						status = validate(Encoding.ASCII.GetString(client_data)); 
					}

					if (status == 0 || (!Encoding.ASCII.GetString(client_data).Contains("\r\n\r\n") && header_size < client_data.Length))
					{
						stream.Dispose();
						return null;
					}

					else if (Encoding.ASCII.GetString(client_data).Contains("\r\n\r\n") && status == 2)										//if the new input finishes the header read
					{
						string header_data = Encoding.ASCII.GetString(client_data);

						WebRequest request = new WebRequest();

						int length = -1;
						int top_end = header_data.IndexOf("\r\n") + 2;
						int tail_end = header_data.IndexOf("\r\n\r\n") + 4;

						string head = header_data.Substring(0, top_end);
						string tail = header_data.Substring(top_end, tail_end - top_end);


						byte[] body = new byte[client_data.Length - tail_end];
						Array.Copy(client_data, tail_end, body, 0, body.Length);


						string [] argument_tokens = head.Split(new string [] { " ","\r\n" }, StringSplitOptions.RemoveEmptyEntries);
						string [] request_tokens = tail.Split(new string [] {"\r\n" }, StringSplitOptions.RemoveEmptyEntries);

						request.Http_Method = argument_tokens[0];
						request.Request_Target = Uri.UnescapeDataString(argument_tokens[1]);
						request.Http_Version = argument_tokens[2];

						foreach(string token in request_tokens)
						{
							string [] header_tokens = token.Split(new string [] {": " }, StringSplitOptions.RemoveEmptyEntries);
							request.Define(header_tokens[0], header_tokens[1]);

							if (header_tokens[0] == "Content-Length")
							{
								length = Convert.ToInt32(header_tokens[1]);
							}
						}

						request.Network_Stream = stream;

						if (-1 < length)
						{ 
							request.Body = new ConcatStream(new MemoryStream(body), stream, length);
						}
						else 
						{ 
							request.Body = new ConcatStream(new MemoryStream(body), stream);
						}

						return request;
					} 
				}
			}
			catch(Exception e)
			{
				stream.Dispose();
				return null;
			}
			stream.Dispose();
			return null;
		}


		/*
		public static WebRequest BuildRequest(TcpClient client)
		{
			byte[] buffer = new byte[128];
			string client_data = "";

			int num_bytes;
			int tot = 0;
			int status = -1;

			NetworkStream stream = client.GetStream();
			stream.ReadTimeout = 1000;

			try 
			{

				DateTime start = DateTime.Now;

				while((num_bytes = stream.Read(buffer, 0, buffer.Length)) != 0) 
				{
					tot += num_bytes;

					if (10 <= (DateTime.Now - start).TotalSeconds) { throw new IOException(); }

					client_data += System.Text.Encoding.ASCII.GetString(buffer, 0, num_bytes);

					if (status != 2)
					{
						status = validate(client_data);
					}
						
					if (status == 0 || (!client_data.Contains("\r\n\r\n") && header_size < client_data.Length))
					{
						stream.Dispose();
						return null;
					}
					else if (client_data.Contains("\r\n\r\n") && status == 2)										//if the new input finishes the header read
					{

						Console.WriteLine(client_data.Length);
						Console.WriteLine(tot);

						WebRequest request = new WebRequest();

						int length = -1;
						int top_end = client_data.IndexOf("\r\n") + 2;
						int tail_end = client_data.IndexOf("\r\n\r\n") + 4;

						string head = client_data.Substring(0, top_end);
						string tail = client_data.Substring(top_end, tail_end - top_end);
					
						int body_start = (tail_end + num_bytes) - client_data.Length - 1;
						int body_size = num_bytes - body_start;

						byte[] body = new byte[body_size];
						Array.Copy(buffer, body_start, body, 0, body_size);


						string [] argument_tokens = head.Split(new string [] { " ","\r\n" }, StringSplitOptions.RemoveEmptyEntries);
						string [] request_tokens = tail.Split(new string [] {"\r\n" }, StringSplitOptions.RemoveEmptyEntries);

						request.Http_Method = argument_tokens[0];
						request.Request_Target = Uri.UnescapeDataString(argument_tokens[1]);
						request.Http_Version = argument_tokens[2];

						foreach(string token in request_tokens)
						{
							string [] header_tokens = token.Split(new string [] {": " }, StringSplitOptions.RemoveEmptyEntries);
							request.Define(header_tokens[0], header_tokens[1]);

							if (header_tokens[0] == "Content-Length")
							{
								length = Convert.ToInt32(header_tokens[1]);
							}
						}

						request.Network_Stream = stream;

						if (-1 < length)
						{ 
							request.Body = new ConcatStream(new MemoryStream(body), stream, length);
						}
						else 
						{ 
							request.Body = new ConcatStream(new MemoryStream(body), stream);
						}

						return request;
					}
				}
			}
			catch(Exception e)
			{
				stream.Dispose();
				return null;
			}
			stream.Dispose();
			return null;
		}*/

			
		private static int validate(string tcp_client_data)
		{
			string pattern = @"^G(E(T( (\S+( (H(T(T(P(/(1(\.(1(\r(\n(.|\n)*)?)?)?)?)?)?)?)?)?)?)?)?)?)?)?$";
			string pattern2 = @"^GET \S+ HTTP/1\.1\r\n";

			string pattern3 = @"^P(U(T( (\S+( (H(T(T(P(/(1(\.(1(\r(\n(.|\n)*)?)?)?)?)?)?)?)?)?)?)?)?)?)?)?$";
			string pattern4 = @"^PUT \S+ HTTP/1\.1\r\n";

			if (request_line_size <= tcp_client_data.Length && !tcp_client_data.Contains("\r\n"))
			{
				return 0;
			}

			if (!Regex.Match(tcp_client_data, pattern, RegexOptions.Singleline).Success && 
				!Regex.Match(tcp_client_data, pattern3, RegexOptions.Singleline).Success) { return 0; }			//if not get or put
			else
			{
				if (tcp_client_data.Contains("\n"))
				{
					if (tcp_client_data.Contains("\r\n"))
					{
						if(!Regex.Match(tcp_client_data, pattern2).Success &&
						   !Regex.Match(tcp_client_data, pattern4).Success) { return 0; }						//i fnot get or put
						else { return 2; }
					}
					return 0;
				}
				return 1;
			}
		}
	}
}
