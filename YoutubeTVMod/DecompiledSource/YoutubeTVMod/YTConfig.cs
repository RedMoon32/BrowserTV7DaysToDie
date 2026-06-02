using System;
using System.IO;
using System.Reflection;

namespace YoutubeTVMod;

public class YTConfig
{
	private static readonly string ConfigFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Config", "yt_config.ini");

	public string Username { get; set; }

	public string Password { get; set; }

	public bool ShortsOnly { get; set; }

	public YTConfig()
	{
		LoadConfig();
	}

	private void LoadConfig()
	{
		Username = "";
		Password = "";
		ShortsOnly = false;
		if (!File.Exists(ConfigFilePath))
		{
			return;
		}
		try
		{
			string[] array = File.ReadAllLines(ConfigFilePath);
			bool flag = false;
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text = array2[i].Trim();
				if (string.IsNullOrEmpty(text) || text.StartsWith(";") || text.StartsWith("#"))
				{
					continue;
				}
				if (text.Equals("[YouTubeTV]", StringComparison.OrdinalIgnoreCase))
				{
					flag = true;
				}
				else if (text.StartsWith("[") && text.EndsWith("]"))
				{
					flag = false;
				}
				else
				{
					if (!flag)
					{
						continue;
					}
					int num = text.IndexOf('=');
					if (num <= 0)
					{
						continue;
					}
					string text2 = text.Substring(0, num).Trim();
					string text3 = text.Substring(num + 1).Trim();
					switch (text2.ToLowerInvariant())
					{
					case "username":
						Username = text3;
						break;
					case "password":
						Password = text3;
						break;
					case "shortsonly":
					{
						if (bool.TryParse(text3, out var result))
						{
							ShortsOnly = result;
						}
						break;
					}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public string GetYtDlpArguments()
	{
		string text = "";
		if (!string.IsNullOrEmpty(Username))
		{
			text = text + "--username \"" + Username + "\" ";
		}
		if (!string.IsNullOrEmpty(Password))
		{
			text = text + "--password \"" + Password + "\" ";
		}
		if (ShortsOnly)
		{
			text += "--match-filters \"duration <= 60\" ";
		}
		return text.Trim();
	}
}
