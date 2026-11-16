using UnityEditor.Compilation;

namespace GameLovers.DataImporter.Samples.DataImport.Editor
{
	// A project normally writes its paths as literals ("Assets/Data/Items.csv"). The sample resolves them from
	// its own assembly definition instead, because Package Manager imports it under a versioned folder.
	internal static class SampleData
	{
		private static string _folder;

		public static string Path(string fileName)
		{
			if (_folder == null)
			{
				var assembly = typeof(SampleData).Assembly.GetName().Name;
				var editorFolder = System.IO.Path.GetDirectoryName(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly));

				_folder = System.IO.Path.GetDirectoryName(editorFolder).Replace('\\', '/') + "/Data/";
			}

			return _folder + fileName;
		}
	}
}
