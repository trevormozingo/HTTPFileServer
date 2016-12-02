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
		private bool m_allowUploads = true;

		public FilesWebService(FileSys422 fs)
		{
			this.fs = fs;
		}

		public override void Handler(WebRequest req)
		{

			if (req.Http_Method == "GET")
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


			else if (req.Http_Method == "PUT")
			{
				
				int tot_bytes = 0;
				int num_bytes = 0;
				byte[] buffer = new byte[1024];

				string path = req.Request_Target.Substring(1).Substring(req.Request_Target.Substring(1).IndexOf('/'));

				string [] dirs = Path.GetDirectoryName(path).Split(new char [] { '/' }, StringSplitOptions.RemoveEmptyEntries);

				Dir422 curr = fs.GetRoot();

				foreach (string dir in dirs)
				{
					curr = curr.GetDir(dir);
				}
					
				if (curr.ContainsDir(Path.GetFileName(path), false) || curr.ContainsFile(Path.GetFileName(path), false)) { return; }

				string dest = ((StdFSDir)fs.GetRoot()).PathName + path;

				FileStream outfile = new FileStream(dest, FileMode.OpenOrCreate);

				try 
				{
					while((num_bytes = req.Body.Read(buffer, 0, buffer.Length)) != 0 && tot_bytes != req.Body.Length)
					{
						tot_bytes += num_bytes;
						outfile.Write(buffer, 0, num_bytes);
					}

					req.WriteHTMLResponse("");

					outfile.Close();

				}
				catch (Exception e) { return; }

			}
		}

		private string BuildDirHTML(Dir422 directory)
		{
			string page = "<html>";
			page += "<h1>Folders</h1>";

			if (m_allowUploads) 
			{
				page +=
					@"<script>
					function selectedFileChanged(fileInput, urlPrefix) 
					{

						document.getElementById('uploadHdr').innerText = 'Uploading ' + fileInput.files[0].name + '...';
						
						if (!window.XMLHttpRequest)
						{
							alert('Your browser does not support XMLHttpRequest. Please update your browser.');
							return; 
						}
					
						var uploadControl = document.getElementById('uploader');
 
						if (uploadControl)
						{
						//	uploadControl.style.visibility = 'hidden';
						}

						
						if (urlPrefix.lastIndexOf('/') != urlPrefix.length - 1) 
						{
							urlPrefix += '/'; 
						}
					 		
						var uploadURL = urlPrefix + fileInput.files[0].name;
					
						var req = new XMLHttpRequest(); 
						req.open('PUT', uploadURL); 
						req.onreadystatechange = function() 
						{
							document.getElementById('uploadHdr').innerText = 'Upload (request status == ' + req.status + ')'; 
						};
						req.send(fileInput.files[0]); 
					}
					</script> ";
			}


			foreach (Dir422 dir in directory.GetDirs())
			{
				page += "<a href=" + BuildDirUri(dir) + ">" + dir.Name + "</a><br>";
			}

			page += "<h1>Files</h1>";

			foreach (File422 file in directory.GetFiles())
			{
				page += "<a href=" + BuildFileUri(file) + ">" + file.Name + "</a><br>";
			}

			if (m_allowUploads) 
			{
				page += 
					"<hr><h3 id='uploadHdr'>Upload</h3><br>" +
					"<input id=\"uploader\" type='file' /><br>"  +
					string.Format(
						"<button onclick='selectedFileChanged(document.getElementById(\"uploader\"),\"{0}\")' />Upload</button><hr>", 
						BuildDirUri(directory));
			}

			page += "</html>";

			return page;
		}
			
		private string BuildDirUri(Dir422 directory)
		{
			string uri = "";
			while (directory != null)
			{
				uri = "/" + directory.Name + uri;
				directory = directory.Parent;
			}
			return System.Uri.EscapeUriString(uri);
		}

		private string BuildFileUri(File422 file)
		{
			return BuildDirUri(file.Parent) + "/" + System.Uri.EscapeUriString(file.Name);
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