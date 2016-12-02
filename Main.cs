using System;
using CS422;
 

public class Test
{
	public static void Main()
	{

		FileSys422 fileSystem = StandardFileSystem.Create("/Users/trevormozingo/Desktop/files");
			
		WebService service = new FilesWebService(fileSystem);

		WebServer.AddService(service);

		WebServer.Start(4220, 60);
	
	}
}
