using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Linq;

namespace SIGVerse.Competition.Handyman
{
	[InitializeOnLoad]
	public class ConfigInitializer
	{
		static ConfigInitializer()
		{
			FileInfo configFileInfo = new FileInfo(Application.dataPath + HandymanConfig.FolderPath + "sample/" + HandymanConfig.ConfigFileName);

			if(!configFileInfo.Exists) { return; }

			DirectoryInfo sampleDirectoryInfo = new DirectoryInfo(Application.dataPath + HandymanConfig.FolderPath + "sample/");

			foreach (FileInfo fileInfo in sampleDirectoryInfo.GetFiles().Where(fileinfo => fileinfo.Name != ".gitignore"))
			{
				string destFilePath = Application.dataPath + HandymanConfig.FolderPath + fileInfo.Name;

				if (!File.Exists(destFilePath))
				{
					fileInfo.CopyTo(destFilePath);
				}
			}
		}
	}
}

