using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace CS422
{
	public class ThreadLake : IDisposable
	{
		private List<Thread> threads = new List<Thread>();
		private BlockingCollection<TcpClient> client_collection = new BlockingCollection<TcpClient>();

		public ThreadLake(int thread_count)
		{
			if (thread_count <= 0)
			{
				thread_count = 64;
			}

			for (int i = 0; i < thread_count; i++)
			{
				Thread thread = new Thread(ThreadWork);
				thread.Start();
				threads.Add(thread);
			}
		}

		public List<Thread>Threads
		{
			get { return threads; }
		}

		public void Insert(TcpClient client)
		{
			client_collection.Add(client);
		}
			
		public int Count
		{
			get { return client_collection.Count; }
		}

		private void ThreadWork() 
		{ 
			while (WebServer.server_active)
			{
				TcpClient tcp_client = client_collection.Take();

				if (tcp_client == null)
				{
					return;
				}

				WebRequest request = WebServer.BuildRequest(tcp_client);

				if (request != null)
				{
					bool found = false;

					foreach (WebService service in WebServer.Services)
					{
						if (service.ServiceURI == request.Request_Target || request.Request_Target.StartsWith(service.ServiceURI + "/"))
						{
							service.Handler(request);
							request.Dispose();
							found = true;
						}
					}

					if (!found)
					{
						request.WriteNotFoundResponse(":( Page not found!");
						request.Dispose();
					}
				}
				tcp_client.Close();
			}
		}
			
		public void Dispose()
		{	
			foreach (Thread t in threads)
			{
				t.Abort();
			}
		}
	}
}

