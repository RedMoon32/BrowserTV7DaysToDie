using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class YoutubeTVInit : IModApi
{
	private static bool _assemblyResolverRegistered;

	private static string _modPath;

	public void InitMod(Mod _modInstance)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)"Loading YoutubeTV Mod...");
		_modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		Debug.Log((object)("YoutubeTV Mod: Mod path is " + _modPath));
		RegisterAssemblyResolver();
		RegisterNetworkPackages();
		new Harmony("Yakov.YoutubeTV").PatchAll(Assembly.GetExecutingAssembly());
	}

	private void RegisterAssemblyResolver()
	{
		if (!_assemblyResolverRegistered)
		{
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
			_assemblyResolverRegistered = true;
			Debug.Log((object)"YoutubeTV Mod: Assembly resolver registered successfully.");
			PreloadCriticalAssemblies();
		}
	}

	private void PreloadCriticalAssemblies()
	{
		string path = Path.Combine(_modPath, "python_runtime", "managed");
		string text = Path.Combine(path, "Microsoft.CSharp.dll");
		if (File.Exists(text))
		{
			Assembly.LoadFrom(text);
		}
		else
		{
			text = Path.Combine(_modPath, "Microsoft.CSharp.dll");
			if (File.Exists(text))
			{
				Assembly.LoadFrom(text);
			}
		}
		string text2 = Path.Combine(path, "System.Reflection.Emit.ILGeneration.dll");
		if (File.Exists(text2))
		{
			Assembly.LoadFrom(text2);
		}
		else
		{
			text2 = Path.Combine(_modPath, "System.Reflection.Emit.ILGeneration.dll");
			if (File.Exists(text2))
			{
				Assembly.LoadFrom(text2);
			}
		}
		string text3 = Path.Combine(path, "System.Reflection.Emit.dll");
		if (File.Exists(text3))
		{
			Assembly.LoadFrom(text3);
			return;
		}
		text3 = Path.Combine(_modPath, "System.Reflection.Emit.dll");
		if (File.Exists(text3))
		{
			Assembly.LoadFrom(text3);
		}
	}

	private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
	{
		string name = new AssemblyName(args.Name).Name;
		string[] array = new string[3]
		{
			_modPath,
			Path.Combine(_modPath, "python_runtime", "managed"),
			Path.Combine(_modPath, "python_runtime")
		};
		for (int i = 0; i < array.Length; i++)
		{
			string text = Path.Combine(array[i], name + ".dll");
			if (File.Exists(text))
			{
				return Assembly.LoadFrom(text);
			}
		}
		if (name == "Python.Runtime")
		{
			string text2 = Path.Combine(_modPath, "python_runtime", "managed", "Python.Runtime.dll");
			if (File.Exists(text2))
			{
				return Assembly.LoadFrom(text2);
			}
		}
		if (name.StartsWith("System.Reflection.Emit"))
		{
			string text3 = Path.Combine(_modPath, "python_runtime", "managed", name + ".dll");
			if (File.Exists(text3))
			{
				return Assembly.LoadFrom(text3);
			}
			text3 = Path.Combine(_modPath, name + ".dll");
			if (File.Exists(text3))
			{
				return Assembly.LoadFrom(text3);
			}
		}
		return null;
	}

	private void RegisterNetworkPackages()
	{
		RegisterNetPackage<NetPackageSetVolumeClient>();
		RegisterNetPackage<NetPackageSetYouTubeURLServer>();
		RegisterNetPackage<NetPackageSetYouTubeURLClient>();
		RegisterNetPackage<NetPackageControlPlaybackServer>();
		RegisterNetPackage<NetPackageControlPlaybackClient>();
	}

	private static void RegisterNetPackage<TPackage>() where TPackage : NetPackage
	{
		Type typeFromHandle = typeof(TPackage);
		NetPackageManager.knownPackageTypes[typeFromHandle.Name] = typeFromHandle;
		Debug.Log((object)("YoutubeTV Mod: Registered net package " + typeFromHandle.Name));
	}
}
