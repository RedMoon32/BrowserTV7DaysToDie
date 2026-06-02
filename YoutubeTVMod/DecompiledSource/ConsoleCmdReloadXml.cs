using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Scripting;

[Preserve]
public class ConsoleCmdReloadXml : ConsoleCmdAdmin
{
	public override string[] getCommands()
	{
		return new string[2] { "ReloadXML", "rx" };
	}

	public override string getDescription()
	{
		return "\r\nUsage: ReloadXML {'all' OR XmlFileName1 XmlFileName2 XmlFileName3 ...}\r\n\r\nReloads either all XML if 'all' is given as the first parameter, or if XML\r\nXML file basenames (e.g. vehicles, buffs) are given, the corresponding\r\nfiles are reloaded in the given order, e.g. 'rx vehicles buffs misc' will\r\nreload vehicles, then buffs, then misc.\r\n\r\nReloading 'all' uses a different method to reloading individual files. The\r\n'all' method works more consistently, but takes much longer. Conversely,\r\nnot all individual files even have code to reload during the game and\r\ncannot be reloaded without using 'rx all'. This is why you may get output\r\nsaying that a file couldn't be reloaded.\r\n\r\nPS: If you *do* get error spam trying to load a specific XML, try running\r\n'rx all' immediately and the spam might stop.\r\n\r\nAliases: rx";
	}

	public override string GetHelp()
	{
		return ((ConsoleCmdAbstract)this).getDescription();
	}

	public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
	{
		if (_params.Count == 0)
		{
			Debug.Log((object)((ConsoleCmdAbstract)this).getDescription());
			return;
		}
		if (_params.Count == 1 && Extensions.EqualsCaseInsensitive(_params[0], "all"))
		{
			Debug.Log((object)"Reloading all xml...");
			WorldStaticData.ReloadAllXmlsSync();
			return;
		}
		object[] value = Traverse.Create(typeof(WorldStaticData)).Field("xmlsToLoad").GetValue<object[]>();
		Traverse.Create((object)value);
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		bool flag = false;
		foreach (string _param in _params)
		{
			if (Extensions.EqualsCaseInsensitive(_param, "items"))
			{
				flag = true;
				continue;
			}
			bool flag2 = false;
			object[] array = value;
			foreach (object obj in array)
			{
				Traverse val = Traverse.Create(obj);
				string value2 = val.Field("XmlName").GetValue<string>();
				if (!Extensions.EqualsCaseInsensitive(value2, _param))
				{
					continue;
				}
				Action<XmlFile> value3 = val.Field("ReloadDuringGameMethod").GetValue<Action<XmlFile>>();
				if (value3 == null)
				{
					list2.Add(value2);
					flag2 = true;
					break;
				}
				flag2 = true;
				XmlFile xmlFile = null;
				Debug.Log((object)("Repatching XML '" + _param + "'..."));
				ThreadManager.RunCoroutineSync(XmlPatcher.LoadAndPatchConfig(value2, (Action<XmlFile>)delegate(XmlFile _file)
				{
					xmlFile = _file;
				}));
				Debug.Log((object)("Reloading XML '" + _param + "'..."));
				if (xmlFile != null)
				{
					value3(xmlFile);
				}
				break;
			}
			if (!flag2)
			{
				list.Add(_param);
			}
		}
		foreach (string item in list)
		{
			Log.Error("Failed to find XML '" + item + "'.");
		}
		foreach (string item2 in list2)
		{
			Log.Warning("Couldn't reload XML '" + item2 + "' because it has no method for reloading during the game.");
		}
		if (flag)
		{
			Log.Warning("Even though 'items' has a method for reloading during the game, the method causes error spam. Please reload all if you want to reload items.");
		}
	}
}
