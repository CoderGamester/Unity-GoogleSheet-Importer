using System.Collections.Generic;
using System.IO;
using System.Text;
using GameLoversEditor.GoogleSheetImporter;
using UnityEditor;

namespace SheetImporters
{
	/// <inheritdoc />
	[GoogleSheetImportOrder(0)]
	public class GameIdsImporter : IGoogleSheetImporter
	{
		private const string _name = "GameId";
		private const string _nameGroup = "GameIdGroup";
		private const string _idTag = "Id";
		private const string _groupsTag = "Groups";
		
		/// <inheritdoc />
		public string GoogleSheetUrl => "https://docs.google.com/spreadsheets/d/1pxV7Fp8T9ea-Bp1ts0kn0JwAi1M3RyLpibq4LyOYQT8/edit#gid=1793530949";
		
		/// <inheritdoc />
		public void Import(List<Dictionary<string, string>> data)
		{
			var idList = new List<string>();
			var groupList = new List<string>();
			var mapGroups = new Dictionary<string, List<string>>();
			var mapIds = new Dictionary<string, List<string>>();
			
			foreach (var entry in data)
			{
				var groups = new List<string>(CsvParser.ArrayParse<string>(entry[_groupsTag]));
				var id = GetCleanName(entry[_idTag]);
				
				idList.Add(id);
				mapGroups.Add(id, groups);

				foreach (var group in groups)
				{
					var groupName = GetCleanName(group);
					if (!groupList.Contains(groupName))
					{
						groupList.Add(groupName);
						mapIds.Add(groupName, new List<string>());
					}
					
					mapIds[groupName].Add(id);
				}
			}

			var script = GenerateScript(idList, groupList, mapGroups, mapIds);

			SaveScript(script);
			AssetDatabase.Refresh();
		}

		private static string GenerateScript(IList<string> ids, IList<string> groups, Dictionary<string, List<string>> mapGroups,
			Dictionary<string, List<string>> mapIds)
		{
			var stringBuilder = new StringBuilder();

			stringBuilder.AppendLine("using System.Collections.Generic;");
			stringBuilder.AppendLine("using System.Collections.ObjectModel;");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("/* AUTO GENERATED CODE */");
			stringBuilder.AppendLine("namespace Ids");
			stringBuilder.AppendLine("{");
			
			stringBuilder.AppendLine($"\tpublic enum {_name}");
			stringBuilder.AppendLine("\t{");
			GenerateEnums(stringBuilder, ids);
			stringBuilder.AppendLine("\t}");
			
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\tpublic enum {_nameGroup}");
			stringBuilder.AppendLine("\t{");
			GenerateEnums(stringBuilder, groups);
			stringBuilder.AppendLine("\t}");
			
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\tpublic static class {_name}Lookup");
			stringBuilder.AppendLine("\t{");
			GenerateLoopUpMethods(stringBuilder);
			GenerateLoopUpMaps(stringBuilder, mapGroups, _name, _nameGroup, "groups");
			GenerateLoopUpMaps(stringBuilder, mapIds, _nameGroup, _name, "ids");
			stringBuilder.AppendLine("\t}");
			
			stringBuilder.AppendLine("}");

			return stringBuilder.ToString();
		}

		private static void GenerateLoopUpMethods(StringBuilder stringBuilder)
		{
			stringBuilder.AppendLine($"\t\tpublic static bool IsInGroup(this {_name} id, {_nameGroup} group)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\tif (!_groups.TryGetValue(id, out var groups))");
			stringBuilder.AppendLine("\t\t\t{");
			stringBuilder.AppendLine("\t\t\t\treturn false;");
			stringBuilder.AppendLine("\t\t\t}");
			stringBuilder.AppendLine("\t\t\treturn groups.Contains(group);");
			stringBuilder.AppendLine("\t\t}");
			
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic static IList<{_name}> GetIds(this {_nameGroup} group)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\treturn _ids[group];");
			stringBuilder.AppendLine("\t\t}");
			
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic static IList<{_nameGroup}> GetGroups(this {_name} id)");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine("\t\t\treturn _groups[id];");
			stringBuilder.AppendLine("\t\t}");
			
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic class {_name}Comparer : IEqualityComparer<{_name}>");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine($"\t\t\tpublic bool Equals({_name} x, {_name} y)");
			stringBuilder.AppendLine("\t\t\t{");
			stringBuilder.AppendLine("\t\t\t\treturn x == y;");
			stringBuilder.AppendLine("\t\t\t}");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\t\tpublic int GetHashCode({_name} obj)");
			stringBuilder.AppendLine("\t\t\t{");
			stringBuilder.AppendLine("\t\t\t\treturn (int)obj;");
			stringBuilder.AppendLine("\t\t\t}");
			stringBuilder.AppendLine("\t\t}");
			
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tpublic class {_nameGroup}Comparer : IEqualityComparer<{_nameGroup}>");
			stringBuilder.AppendLine("\t\t{");
			stringBuilder.AppendLine($"\t\t\tpublic bool Equals({_nameGroup} x, {_nameGroup} y)");
			stringBuilder.AppendLine("\t\t\t{");
			stringBuilder.AppendLine("\t\t\t\treturn x == y;");
			stringBuilder.AppendLine("\t\t\t}");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\t\tpublic int GetHashCode({_nameGroup} obj)");
			stringBuilder.AppendLine("\t\t\t{");
			stringBuilder.AppendLine("\t\t\t\treturn (int)obj;");
			stringBuilder.AppendLine("\t\t\t}");
			stringBuilder.AppendLine("\t\t}");
		}

		private static void GenerateLoopUpMaps(StringBuilder stringBuilder, Dictionary<string, List<string>> map,
			string element1Type, string element2Type, string fieldName)
		{
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"\t\tprivate static readonly Dictionary<{element1Type}, ReadOnlyCollection<{element2Type}>> _{fieldName} =");
			stringBuilder.AppendLine($"\t\t\tnew Dictionary<{element1Type}, ReadOnlyCollection<{element2Type}>> (new {element1Type}Comparer())");
			stringBuilder.AppendLine("\t\t\t{");

			foreach (var pair in map)
			{
				stringBuilder.AppendLine("\t\t\t\t{");
				stringBuilder.AppendLine($"\t\t\t\t\t{element1Type}.{pair.Key}, new List<{element2Type}>");
				stringBuilder.AppendLine("\t\t\t\t\t{");
				for (var i = 0; i < pair.Value.Count; i++)
				{
					stringBuilder.Append("\t\t\t\t\t\t");
					stringBuilder.Append($"{element2Type}.{pair.Value[i]}");
					stringBuilder.Append(i + 1 == pair.Value.Count ? "\n" : ",\n");
				}
				stringBuilder.AppendLine("\t\t\t\t\t}.AsReadOnly()");
				stringBuilder.AppendLine("\t\t\t\t},");
			}
			
			stringBuilder.AppendLine("\t\t\t};");
		}

		private static void GenerateEnums(StringBuilder stringBuilder, IList<string> list)
		{
			for (var i = 0; i < list.Count; i++)
			{
				stringBuilder.Append("\t\t");
				stringBuilder.Append(list[i]);
				stringBuilder.Append(i + 1 == list.Count ? "\n" : ",\n");
			}
		}
		
		private static string GetCleanName(string name)
		{
			return name.Replace(' ', '_');
		}

		private static void SaveScript(string scriptString)
		{
			var scriptAssets = AssetDatabase.FindAssets($"t:Script {_name}");
			var scriptPath = $"Assets/{_name}.cs";

			foreach (var scriptAsset in scriptAssets)
			{
				var path = AssetDatabase.GUIDToAssetPath(scriptAsset);
				if (path.EndsWith($"/{_name}.cs"))
				{
					scriptPath = path;
					break;
				}
			}

			File.WriteAllText(scriptPath, scriptString);
		}
	}
}