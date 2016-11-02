using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Net;

namespace CS422
{
	public class FilesWebService : WebService
	{
		private int size = 8 * 1024;
		private FileSys422 fs;

		public FilesWebService(FileSys422 fs)
		{
			this.fs = fs;
		}

		public override void Handler(WebRequest req)
		{

			Object fs_object = FSSearch(req.Request_Target);

			if (fs_object != null)
			{
				if (fs_object.GetType().BaseType == typeof(Dir422))
				{
					req.WriteHTMLResponse(BuildDirHTML(fs_object as Dir422));
				}

				if (fs_object.GetType().BaseType == typeof(File422))
				{
					if (req.Headers.ContainsKey("Range"))
					{
						byte[] data = new byte[size];
						File422 file = fs_object as File422;
						Stream stream = file.OpenReadOnly();

						int range_case = 0;
						int start = 0, end = 0, length = 0;

						string range = req.Headers["Range"];
						string range_values = range.Substring(6);
		
						range_case = (Regex.Match(range_values, @"^-[0-9]+$").Success? 1 : range_case);
						range_case = (Regex.Match(range_values, @"^[0-9]+-$").Success? 2 : range_case);
						range_case = (Regex.Match(range_values, @"^[0-9]+-[0-9]+$").Success? 3 : range_case);

						string[] section = range_values.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

						if (range_case == 1) 
						{
							Int32.TryParse(section[0], out start);
							length = start;
							end = (int)stream.Length - 1;
							start = (int)stream.Length - start;
						}

						if (range_case == 2) 
						{
							Int32.TryParse(section[0], out start);
							end = (int)stream.Length - 1;
							length = (int)stream.Length - start; 
						}

						if (range_case == 3) 
						{
							Int32.TryParse(section[0], out start);
							Int32.TryParse(section[1], out end);
							length = end - start;
						}
							
						try
						{

							req.WriteHTMLPartialResponseHeader(Path.GetExtension(file.Name), start, end, length, stream.Length);
							stream.Seek(start, SeekOrigin.Begin);

							int toRead = size;

							if (length < size) { toRead = length; }

							int n;
							while ((n = stream.Read(data, 0, toRead)) != 0)
							{
								req.WriteHTMLResponseBody(data, n);
								length -= n;
								if (length < size) { toRead = length; }
							}
						}
						catch(Exception e) { return; }

					}
					else
					{
						byte[] data = new byte[size];
						File422 file = fs_object as File422;
						Stream stream = file.OpenReadOnly();

						try
						{
							req.WriteHTMLResponseHeader(Path.GetExtension(file.Name), stream.Length);
							int n;
							while ((n = stream.Read(data, 0, data.Length)) != 0)
							{
								req.WriteHTMLResponseBody(data, n);
							}
							stream.Dispose();
						}
						catch(Exception e) 
						{ 
							stream.Dispose();
							return; 
						}
					}
				}
			}
			else
			{
				req.WriteNotFoundResponse(":( Page not found!");
			}
		}

		private string BuildDirHTML(Dir422 directory)
		{
			string page = "<html>";
			page += "<h1>Folders</h1>";

			foreach (Dir422 dir in directory.GetDirs())
			{
				page += "<a href=" + BuildDirUri(dir) + ">" + dir.Name + "</a><br>";
			}

			page += "<h1>Files</h1>";

			foreach (File422 file in directory.GetFiles())
			{
				Console.WriteLine(BuildFileUri(file));
				page += "<a href=" + BuildFileUri(file) + ">" + file.Name + "</a><br>";
			}

			page += "</html>";

			return page;
		}

		private string BuildFileUri(File422 file)
		{
			return BuildDirUri(file.Parent) + WebUtility.UrlEncode("/" + file.Name);
		}

		private string BuildDirUri(Dir422 directory)
		{
			string uri = "";

			while (directory != null)
			{
				uri = "/" + directory.Name + WebUtility.UrlEncode(uri);
				directory = directory.Parent;
			}
			return uri;
		}

		private Object FSSearch(string uri)
		{
			Dir422 curr_dir = fs.GetRoot();

			if (uri == ServiceURI || uri == ServiceURI + "/") { return curr_dir; }

			uri = uri.Substring(ServiceURI.Length);
			List<string> names = new List<string>(uri.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries));

			while (1 < names.Count)
			{
				curr_dir = curr_dir.GetDir(names[0]);

				if (curr_dir == null) { return null; }

				names.RemoveAt(0);
			}

			Dir422 dir = curr_dir.GetDir(names[0]);
			File422 file = curr_dir.GetFile(names[0]);

			return (dir != null? (object)dir : (object)file);
		}

		public override string ServiceURI 
		{
			get { return "/files"; }
		}
	}
}