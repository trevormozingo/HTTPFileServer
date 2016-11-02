using System;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CS422
{
	public class WebRequest
	{ 
		private ConcurrentDictionary<string, string> headers;
		private Dictionary<string, string> content_types;

		private string http_method;
		private string request_target;
		private string http_version;

		private Stream body;
		private Stream network_stream;

		public WebRequest()
		{
			headers = new ConcurrentDictionary<string, string>();

			content_types = new Dictionary<string, string>()
			{
				{ ".txt" , "text/plain" },
				{ ".jpg" , "image/jpg" }, 
				{ ".png" , "image/png" }, 
				{ ".pdf" , "application/pdf" }, 
				{ ".mp4" , "video/mp4" }

			};

		}

		public string Http_Method
		{
			set { http_method = value; }
			get { return http_method; }
		}

		public string Request_Target
		{
			set { request_target = value; }
			get { return request_target; }
		}

		public string Http_Version
		{
			set { http_version = value; }
			get { return http_version; }
		}

		public Stream Body
		{
			set { body = value; }
			get { return body; }
		}

		public Stream Network_Stream
		{
			set { network_stream = value; }
			get { return network_stream; }
		}
			
		public ConcurrentDictionary<string, string> Headers
		{
			get { return headers; }
		}
			 
		public void Define(string header_name, string header_value)
		{
			headers[header_name] = header_value;
		}

		public void WriteNotFoundResponse(string pageHTML)
		{
			string response = Http_Version 
				+ " 404 BAD REQUEST\r\n" 
				+ "Content-Type: text/html\r\n" 
				+ String.Format("Content-Length: {0}\r\n", pageHTML.Length)
				+ "\r\n"
				+ pageHTML;

			Network_Stream.Write(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetBytes(response).Length);
		}

		public bool WriteHTMLResponse(string htmlString)
		{
			string response = Http_Version 
				+ " 200 OK\r\n" 
				+ "Content-Type: text/html\r\n" 
				+ String.Format("Content-Length: {0}\r\n", htmlString.Length) +
				"\r\n"
				+ htmlString;

			Network_Stream.Write(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetBytes(response).Length);
			return true;
		}

		public bool WriteHTMLResponseHeader(string file_ext, long length)
		{
			string file_type = "text/plain";

			if (content_types.ContainsKey(file_ext))
			{
				file_type = content_types[file_ext];
			}

			string response = Http_Version 
				+ " 200 OK\r\n" 
				+ "Content-Type: " + file_type + "\r\n" 
				+ String.Format("Content-Length: {0}\r\n", length) +
				"\r\n";

			Network_Stream.Write(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetBytes(response).Length);
			return true;
		}

		public bool WriteHTMLResponseBody(byte[] body, int n)
		{
			Network_Stream.Write(body, 0, n);
			return true;
		}
			

		public bool WriteHTMLPartialResponseHeader(string file_ext, int start, int end, long length, long streamLength)
		{
			string file_type = "text/plain";

			if (content_types.ContainsKey(file_ext))
			{
				file_type = content_types[file_ext];
			}

			string response = Http_Version 
				+ " 206 Partial Content\r\n" 
				+ "Content-Type: " + file_type + "\r\n" 
				+ "Content-Range: bytes " + start.ToString() + "-" + end.ToString() + "/" + streamLength.ToString() + "\r\n" 
				+ String.Format("Content-Length: {0}\r\n", length) +
				"\r\n";

			Network_Stream.Write(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetBytes(response).Length);
			return true;
		}

		public void Dispose()
		{
			Network_Stream.Dispose();
		}
	}
}
