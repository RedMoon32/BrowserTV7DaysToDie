using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Networking;
using UnityEngine.Video;
using YoutubeTVMod;

[RequireComponent(typeof(VideoPlayer))]
[RequireComponent(typeof(AudioSource))]
public class YouTubeTVController : MonoBehaviour
{
	public static class YtDlpInitializer
	{
		private static bool _isInitialized;

		private static bool _initializationFailed;

		private static bool _initializationInProgress;

		private static readonly object _initLock = new object();

		private static string _lastError = null;

		public static bool IsInitialized => _isInitialized;

		public static bool InitializationFailed => _initializationFailed;

		public static string LastError => _lastError;

		public static void Initialize()
		{
			lock (_initLock)
			{
				if (_isInitialized || _initializationFailed || _initializationInProgress)
				{
					return;
				}
				_initializationInProgress = true;
			}
			try
			{
				string text = FindExternalYtDlpExecutable();
				if (string.IsNullOrEmpty(text))
				{
					_lastError = "External yt-dlp was not found on PATH or common Homebrew locations.";
					_initializationFailed = true;
					return;
				}
				Debug.Log((object)("[YouTubeTV] Using external yt-dlp: " + text));
				_isInitialized = true;
				PythonTaskRunner.Initialize();
			}
			catch (Exception ex5)
			{
				_lastError = "Failed to initialise external yt-dlp runner: " + ex5.Message;
				_initializationFailed = true;
			}
			finally
			{
				lock (_initLock)
				{
					_initializationInProgress = false;
				}
			}
		}

		public static bool WaitForInitialization(int timeoutMs = 10000)
		{
			int num = 0;
			while (_initializationInProgress && num < timeoutMs)
			{
				Thread.Sleep(100);
				num += 100;
			}
			return _isInitialized;
		}

		public static void Shutdown()
		{
			if (_isInitialized)
			{
				try
				{
					PythonTaskRunner.Shutdown();
				}
				catch (Exception)
				{
				}
				_isInitialized = false;
			}
		}
	}

	public static class PythonTaskRunner
	{
		private static readonly BlockingCollection<Action> _workQueue = new BlockingCollection<Action>();

		private static Thread _pythonThread;

		private static volatile bool _isRunning;

		private static volatile bool _isReady;

		private static readonly object _lock = new object();

		public static bool IsInitialized
		{
			get
			{
				if (_pythonThread != null)
				{
					return _pythonThread.IsAlive;
				}
				return false;
			}
		}

		public static bool IsReady => _isReady;

		public static void Initialize()
		{
			lock (_lock)
			{
				if (_pythonThread == null || !_pythonThread.IsAlive)
				{
					_isRunning = true;
					_pythonThread = new Thread(PythonThreadLoop)
					{
						Name = "PythonWorkerThread",
						IsBackground = true
					};
					_pythonThread.Start();
				}
			}
		}

		private static void PythonThreadLoop()
		{
			try
			{
				_isReady = true;
				while (_isRunning)
				{
					try
					{
						if (_workQueue.TryTake(out var item, TimeSpan.FromMilliseconds(100.0)))
						{
							try
							{
								item();
							}
							catch (Exception)
							{
							}
						}
					}
					catch (OperationCanceledException)
					{
						break;
					}
					catch (Exception)
					{
					}
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				_isReady = false;
			}
		}

		public static Task<T> RunAsync<T>(Func<T> pythonWork)
		{
			TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
			if (!_isReady)
			{
				tcs.SetException(new InvalidOperationException("PythonTaskRunner is not ready"));
				return tcs.Task;
			}
			_workQueue.Add(delegate
			{
				try
				{
					T result = pythonWork();
					tcs.SetResult(result);
				}
				catch (Exception exception)
				{
					tcs.SetException(exception);
				}
			});
			return tcs.Task;
		}

		public static Task RunAsync(Action pythonWork)
		{
			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
			if (!_isReady)
			{
				tcs.SetException(new InvalidOperationException("PythonTaskRunner is not ready"));
				return tcs.Task;
			}
			_workQueue.Add(delegate
			{
				try
				{
					pythonWork();
					tcs.SetResult(result: true);
				}
				catch (Exception exception)
				{
					tcs.SetException(exception);
				}
			});
			return tcs.Task;
		}

		public static void Shutdown()
		{
			lock (_lock)
			{
				if (_isRunning)
				{
					_isRunning = false;
					if (_pythonThread != null && _pythonThread.IsAlive)
					{
						_pythonThread.Join(2000);
					}
					_pythonThread = null;
				}
			}
		}
	}

	private const string YtDlpFormatSelector = "best[vcodec!=none][acodec!=none][ext=mp4][protocol=https]/best[vcodec!=none][acodec!=none][ext=mp4][protocol=http]/best[vcodec!=none][acodec!=none][protocol=https]/best[vcodec!=none][acodec!=none][protocol=http]/best[vcodec!=none][ext=mp4]/best[vcodec!=none]";

	private static string GetModDirectory()
	{
		return Path.GetDirectoryName(typeof(YouTubeTVController).Assembly.Location);
	}

	private static string FindExternalPythonExecutable()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("YOUTUBETV_PYTHON");
		string[] array = new string[5]
		{
			environmentVariable,
			"/opt/homebrew/bin/python3",
			"/usr/local/bin/python3",
			"/usr/bin/python3",
			"python3"
		};
		foreach (string text in array)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			if (Path.IsPathRooted(text) && !File.Exists(text))
			{
				continue;
			}
			if (RunProcess(text, "--version", 5, out var output, out var error) == 0)
			{
				Debug.Log((object)("[YouTubeTV] Found python executable: " + text + " " + (output + error).Trim()));
				return text;
			}
		}
		return null;
	}

	private static string FindExternalYtDlpExecutable()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("YOUTUBETV_YTDLP");
		string[] array = new string[4]
		{
			environmentVariable,
			"/opt/homebrew/bin/yt-dlp",
			"/usr/local/bin/yt-dlp",
			"yt-dlp"
		};
		foreach (string text in array)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			if (Path.IsPathRooted(text) && !File.Exists(text))
			{
				continue;
			}
			if (RunProcess(text, "--version", 5, out var output, out var error) == 0)
			{
				Debug.Log((object)("[YouTubeTV] Found yt-dlp executable: " + text + " " + (output + error).Trim()));
				return text;
			}
		}
		return null;
	}

	private static void AddYtDlpEnvironment(System.Diagnostics.ProcessStartInfo startInfo)
	{
		string modDirectory = GetModDirectory();
		string text = Path.Combine(modDirectory, "lib", "yt-dlp");
		string text2 = Path.Combine(modDirectory, "plugin");
		string text3 = startInfo.EnvironmentVariables["PYTHONPATH"];
		List<string> list = new List<string>();
		if (Directory.Exists(text))
		{
			list.Add(text);
		}
		if (Directory.Exists(text2))
		{
			list.Add(text2);
		}
		if (!string.IsNullOrEmpty(text3))
		{
			list.Add(text3);
		}
		startInfo.EnvironmentVariables["PYTHONPATH"] = string.Join(Path.PathSeparator.ToString(), list.ToArray());
	}

	private static int RunProcess(string fileName, string arguments, int timeoutSeconds, out string output, out string error)
	{
		output = "";
		error = "";
		try
		{
			using (System.Diagnostics.Process process = new System.Diagnostics.Process())
			{
				process.StartInfo = new System.Diagnostics.ProcessStartInfo
				{
					FileName = fileName,
					Arguments = arguments,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};
				process.Start();
				if (!process.WaitForExit(timeoutSeconds * 1000))
				{
					try
					{
						process.Kill();
					}
					catch
					{
					}
					error = "Process timed out";
					return -1;
				}
				output = process.StandardOutput.ReadToEnd();
				error = process.StandardError.ReadToEnd();
				return process.ExitCode;
			}
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return -1;
		}
	}

	private static string ExtractStreamingUrlWithExternalYtDlp(string youtubeURL, string goName)
	{
		string text = FindExternalYtDlpExecutable();
		if (string.IsNullOrEmpty(text))
		{
			Debug.LogError((object)("[YouTubeTV] Cannot find yt-dlp for " + goName));
			return null;
		}
		youtubeURL = NormalizeYouTubeUrlForYtDlp(youtubeURL);
		string text2 = FindYtDlpCookiesFile();
		string text3 = string.IsNullOrEmpty(text2) ? "" : (" --cookies " + QuoteProcessArgument(text2));
		if (!string.IsNullOrEmpty(text2))
		{
			Debug.Log((object)("[YouTubeTV] Using yt-dlp cookies file: " + text2));
		}
		string arguments = "-g --no-warnings --no-check-certificate --no-playlist --geo-bypass --socket-timeout 15" + text3 + " -f " + QuoteProcessArgument(YtDlpFormatSelector) + " " + QuoteProcessArgument(youtubeURL);
		Debug.Log((object)("[YouTubeTV] Running external yt-dlp for " + goName));
		int num = RunProcess(text, arguments, 60, out var output, out var error);
		if (num != 0)
		{
			Debug.LogError((object)("[YouTubeTV] yt-dlp failed for " + goName + ": " + error));
			return null;
		}
		string[] array = output.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		string text4 = null;
		string[] array2 = array;
		foreach (string text5 in array2)
		{
			string text6 = text5.Trim();
			if (text6.Length == 0)
			{
				continue;
			}
			if (text4 == null)
			{
				text4 = text6;
			}
			if (!text6.Contains(".m3u8") && !text6.Contains("/manifest/") && !text6.Contains(".mpd"))
			{
				return text6;
			}
		}
		return text4;
	}

	private static string FindYtDlpCookiesFile()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("YOUTUBETV_COOKIES");
		string modDirectory = GetModDirectory();
		string[] array = new string[4]
		{
			environmentVariable,
			Path.Combine(modDirectory, "www.youtube.com_cookies.txt"),
			Path.Combine(modDirectory, "youtube.com_cookies.txt"),
			Path.Combine(modDirectory, "cookies.txt")
		};
		foreach (string text in array)
		{
			if (!string.IsNullOrEmpty(text) && File.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	private static string QuoteProcessArgument(string value)
	{
		return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
	}

	private static string NormalizeYouTubeUrlForYtDlp(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return url;
		}
		try
		{
			Uri uri = new Uri(url);
			if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
			{
				string text = uri.AbsolutePath.Trim('/');
				int num = text.IndexOf('/');
				if (num >= 0)
				{
					text = text.Substring(0, num);
				}
				if (!string.IsNullOrEmpty(text))
				{
					return "https://www.youtube.com/watch?v=" + text;
				}
			}
		}
		catch
		{
		}
		return url;
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass65_0
	{
		public string youtubeURL;

		public string goName;

		internal string _003CDownloadAndPlayVideo_003Eb__0()
		{
			return ExtractStreamingUrlWithExternalYtDlp(youtubeURL, goName);
		}
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass75_0
	{
		public string capturedShortsPath;

		public string capturedDownloadId;

		public string downloadId;

		internal void _003CDownloadShortsSet_003Eb__0()
		{
			try
			{
				dynamic val = Py.Import("yt_dlp");
				dynamic val2 = Py.Import("builtins");
				PythonEngine.RunSimpleString("\r\ndef shorts_filter(info, *, incomplete):\r\n    duration = info.get('duration')\r\n    if duration and duration >= 60:\r\n        return 'Video is too long'\r\n");
				dynamic val3 = Py.Import("__main__");
				dynamic attr = val3.GetAttr("shorts_filter");
				dynamic val4 = val3.MyLogger();
				dynamic val5 = val.YoutubeDL(new PyDict
				{
					["format"] = new PyString("best[vcodec!=none][ext=mp4]/best[vcodec!=none][acodec!=none][ext=mp4]"),
					["outtmpl"] = new PyString(capturedShortsPath + "/" + capturedDownloadId + ".%(ext)s"),
					["restrictfilenames"] = val2.True,
					["nowritejson"] = val2.True,
					["noplaylist"] = val2.True,
					["max_downloads"] = new PyInt(3),
					["socket_timeout"] = new PyInt(15),
					["retries"] = new PyInt(1),
					["fragment_retries"] = new PyInt(1),
					["quiet"] = val2.True,
					["no_warnings"] = val2.True,
					["nocheckcertificate"] = val2.True,
					["cache_dir"] = val2.False,
					["geo_bypass"] = val2.True,
					["match_filter"] = attr,
					["playlistrandom"] = val2.True,
					["logger"] = val4
				});
				PyList pyList = new PyList();
				pyList.Append(new PyString("ytsearch10:shorts"));
				val5.download(pyList);
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Python yt-dlp error in DownloadShortsSet: " + ex.Message));
			}
		}

		internal bool _003CDownloadShortsSet_003Eb__1(string f)
		{
			return f.Contains(downloadId);
		}
	}

	[CompilerGenerated]
	private sealed class _003CDownloadAndPlayVideo_003Ed__65 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public string youtubeURL;

		public YouTubeTVController _003C_003E4__this;

		private _003C_003Ec__DisplayClass65_0 _003C_003E8__1;

		private float _003CinitWaitTime_003E5__2;

		private float _003CmaxInitWait_003E5__3;

		private float _003CwaitTime_003E5__4;

		private string _003CstreamingUrl_003E5__5;

		private bool _003Csuccess_003E5__6;

		private Task<string> _003CextractionTask_003E5__7;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CDownloadAndPlayVideo_003Ed__65(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E8__1 = null;
			_003CstreamingUrl_003E5__5 = null;
			_003CextractionTask_003E5__7 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_019f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a9: Expected O, but got Unknown
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Expected O, but got Unknown
			int num = _003C_003E1__state;
			YouTubeTVController youTubeTVController = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E8__1 = new _003C_003Ec__DisplayClass65_0();
				_003C_003E8__1.youtubeURL = youtubeURL;
				Debug.Log((object)("Starting streaming of: " + _003C_003E8__1.youtubeURL + " for " + ((Object)((Component)youTubeTVController).gameObject).name));
				_003CinitWaitTime_003E5__2 = 0f;
				_003CmaxInitWait_003E5__3 = 20f;
				goto IL_00d7;
			case 1:
				_003C_003E1__state = -1;
				_003CinitWaitTime_003E5__2 += 1f;
				goto IL_00d7;
			case 2:
				_003C_003E1__state = -1;
				_003CwaitTime_003E5__4 += 0.5f;
				goto IL_01cb;
			case 3:
				{
					_003C_003E1__state = -1;
					goto IL_0369;
				}
				IL_00d7:
				if (!YtDlpInitializer.IsInitialized && !YtDlpInitializer.InitializationFailed && _003CinitWaitTime_003E5__2 < _003CmaxInitWait_003E5__3)
				{
					Debug.Log((object)$"[YouTubeTV] Waiting for Python initialization before playing video... ({_003CinitWaitTime_003E5__2:F1}s)");
					_003C_003E2__current = (object)new WaitForSeconds(1f);
					_003C_003E1__state = 1;
					return true;
				}
				if (!YtDlpInitializer.IsInitialized)
				{
					string text = (YtDlpInitializer.InitializationFailed ? ("Python engine initialization failed: " + (YtDlpInitializer.LastError ?? "Unknown error")) : "Python engine initialization timed out");
					Debug.LogError((object)("[YouTubeTV] Cannot play video - " + text));
					if ((Object)(object)youTubeTVController.tvScreenRenderer != (Object)null && (Object)(object)youTubeTVController.idlePlaceholderTexture != (Object)null)
					{
						youTubeTVController.tvScreenRenderer.material.SetTexture(youTubeTVController.tvScreenMaterialPropertyName, (Texture)(object)youTubeTVController.idlePlaceholderTexture);
					}
					youTubeTVController.downloadCoroutine = null;
					return false;
				}
				_003CwaitTime_003E5__4 = 0f;
				goto IL_01cb;
				IL_01cb:
				if (!PythonTaskRunner.IsReady && _003CwaitTime_003E5__4 < 10f)
				{
					Debug.Log((object)$"[YouTubeTV] Waiting for PythonTaskRunner to be ready... ({_003CwaitTime_003E5__4:F1}s)");
					_003C_003E2__current = (object)new WaitForSeconds(0.5f);
					_003C_003E1__state = 2;
					return true;
				}
				if (!PythonTaskRunner.IsReady)
				{
					Debug.LogError((object)("[YouTubeTV] PythonTaskRunner not ready after 10 seconds. IsInitialized: " + PythonTaskRunner.IsInitialized));
					if ((Object)(object)youTubeTVController.tvScreenRenderer != (Object)null && (Object)(object)youTubeTVController.idlePlaceholderTexture != (Object)null)
					{
						youTubeTVController.tvScreenRenderer.material.SetTexture(youTubeTVController.tvScreenMaterialPropertyName, (Texture)(object)youTubeTVController.idlePlaceholderTexture);
					}
					youTubeTVController.downloadCoroutine = null;
					return false;
				}
				Debug.Log((object)("[YouTubeTV] Python engine ready, extracting video URL for: " + _003C_003E8__1.youtubeURL));
				_003CstreamingUrl_003E5__5 = null;
				_003Csuccess_003E5__6 = false;
				_003CextractionTask_003E5__7 = null;
				try
				{
					_003C_003E8__1.goName = ((Object)((Component)youTubeTVController).gameObject).name;
					_003CextractionTask_003E5__7 = PythonTaskRunner.RunAsync(delegate
						{
							try
							{
								return ExtractStreamingUrlWithExternalYtDlp(_003C_003E8__1.youtubeURL, _003C_003E8__1.goName);
								dynamic val = Py.Import("yt_dlp");
								dynamic val2 = Py.Import("builtins");
								dynamic val3 = Py.Import("__main__");
							dynamic val4 = val3.MyLogger();
							dynamic val5 = val.YoutubeDL(new PyDict
							{
								["format"] = new PyString("best[vcodec!=none][acodec!=none][ext=mp4][protocol=https]/best[vcodec!=none][acodec!=none][ext=mp4][protocol=http]/best[vcodec!=none][acodec!=none][protocol=https]/best[vcodec!=none][acodec!=none][protocol=http]/best[vcodec!=none][ext=mp4]/best[vcodec!=none]"),
								["quiet"] = val2.True,
								["no_warnings"] = val2.True,
								["nocheckcertificate"] = val2.True,
								["cache_dir"] = val2.False,
								["restrictfilenames"] = val2.True,
								["nowritejson"] = val2.True,
								["noplaylist"] = val2.True,
								["socket_timeout"] = new PyInt(15),
								["geo_bypass"] = val2.True,
								["prefer_ffmpeg"] = val2.False,
								["logger"] = val4
							});
							Debug.Log((object)("[YouTubeTV] Calling extract_info for " + _003C_003E8__1.youtubeURL));
							dynamic val6 = val5.extract_info(_003C_003E8__1.youtubeURL, download: false);
							Debug.Log((object)"[YouTubeTV] extract_info returned");
							if (val6 == null)
							{
								return (string)null;
							}
							PyObject pyObject = val6.get("url") as PyObject;
							if (pyObject != null && !pyObject.IsNone())
							{
								string text2 = pyObject.As<string>();
								if (!text2.Contains(".m3u8") && !text2.Contains("/manifest/"))
								{
									return text2;
								}
								Debug.LogWarning((object)"[YouTubeTV] Selected format returned HLS/DASH manifest, searching for direct URL");
							}
							PyObject pyObject2 = val6.get("formats") as PyObject;
							if (pyObject2 != null && !pyObject2.IsNone() && pyObject2.Length() > 0)
							{
								int num2 = (int)pyObject2.Length();
								string text3 = null;
								string arg = null;
								int num3 = 0;
								for (int num4 = num2 - 1; num4 >= 0; num4--)
								{
									dynamic val7 = pyObject2[num4];
									PyObject pyObject3 = val7.get("vcodec") as PyObject;
									if (!(pyObject3 == null) && !pyObject3.IsNone())
									{
										string text4 = pyObject3.As<string>();
										if (!string.IsNullOrEmpty(text4) && !(text4 == "none"))
										{
											PyObject pyObject4 = val7.get("acodec") as PyObject;
											string text5 = null;
											if (pyObject4 != null && !pyObject4.IsNone())
											{
												text5 = pyObject4.As<string>();
											}
											bool flag = !string.IsNullOrEmpty(text5) && text5 != "none";
											PyObject pyObject5 = val7.get("protocol") as PyObject;
											string text6 = null;
											if (pyObject5 != null && !pyObject5.IsNone())
											{
												text6 = pyObject5.As<string>();
											}
											if (string.IsNullOrEmpty(text6) || (!text6.Contains("m3u8") && !text6.Contains("dash")))
											{
												PyObject pyObject6 = val7.get("url") as PyObject;
												if (!(pyObject6 == null) && !pyObject6.IsNone())
												{
													string text7 = pyObject6.As<string>();
													if (!text7.Contains(".m3u8") && !text7.Contains("/manifest/") && !text7.Contains(".mpd"))
													{
														int num5 = 0;
														PyObject pyObject7 = val7.get("height") as PyObject;
														if (pyObject7 != null && !pyObject7.IsNone())
														{
															try
															{
																num5 = pyObject7.As<int>();
															}
															catch
															{
															}
														}
														if (flag && (text3 == null || num5 > num3))
														{
															text3 = text7;
															arg = text4;
															num3 = num5;
															Debug.Log((object)$"[YouTubeTV] Found candidate: vcodec={text4}, acodec={text5}, height={num5}, protocol={text6}");
														}
														else if (!flag && text3 == null)
														{
															text3 = text7;
															arg = text4;
															num3 = num5;
															Debug.Log((object)$"[YouTubeTV] Found video-only candidate: vcodec={text4}, height={num5}, protocol={text6}");
														}
													}
												}
											}
										}
									}
								}
								if (text3 != null)
								{
									Debug.Log((object)$"[YouTubeTV] Selected format: vcodec={arg}, height={num3}");
									return text3;
								}
								dynamic val8 = pyObject2[num2 - 1];
								PyObject pyObject8 = val8.get("url") as PyObject;
								if (pyObject8 != null && !pyObject8.IsNone())
								{
									string text8 = pyObject8.As<string>();
									Debug.LogWarning((object)("[YouTubeTV] No suitable direct format found, using fallback: " + text8));
									return text8;
								}
							}
							return (string)null;
						}
						catch (Exception ex3)
						{
							Debug.LogError((object)("Python yt-dlp error for " + _003C_003E8__1.goName + ": " + ex3.Message));
							Debug.LogError((object)("Stack trace: " + ex3.StackTrace));
							return (string)null;
						}
					});
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("[YouTubeTV] Failed to start Python extraction task: " + ex.Message));
					Debug.LogError((object)("Stack trace: " + ex.StackTrace));
					if ((Object)(object)youTubeTVController.tvScreenRenderer != (Object)null && (Object)(object)youTubeTVController.idlePlaceholderTexture != (Object)null)
					{
						youTubeTVController.tvScreenRenderer.material.SetTexture(youTubeTVController.tvScreenMaterialPropertyName, (Texture)(object)youTubeTVController.idlePlaceholderTexture);
					}
					youTubeTVController.downloadCoroutine = null;
					return false;
				}
				if (_003CextractionTask_003E5__7 != null)
				{
					goto IL_0369;
				}
				Debug.LogError((object)"[YouTubeTV] Extraction task is null, cannot wait for completion");
				break;
				IL_0369:
				if (!_003CextractionTask_003E5__7.IsCompleted)
				{
					if ((Object)(object)youTubeTVController == (Object)null || !((Behaviour)youTubeTVController).enabled || !((Component)youTubeTVController).gameObject.activeInHierarchy)
					{
						Debug.LogWarning((object)"[YouTubeTV] Component became invalid while waiting for extraction. Aborting.");
						return false;
					}
					_003C_003E2__current = null;
					_003C_003E1__state = 3;
					return true;
				}
				if (_003CextractionTask_003E5__7.IsFaulted)
				{
					Debug.LogError((object)$"DownloadAndPlayVideo task failed: {_003CextractionTask_003E5__7.Exception}");
					if (_003CextractionTask_003E5__7.Exception == null)
					{
						break;
					}
					foreach (Exception innerException in _003CextractionTask_003E5__7.Exception.InnerExceptions)
					{
						Debug.LogError((object)("Inner exception: " + innerException.Message + "\n" + innerException.StackTrace));
					}
				}
				else
				{
					try
					{
						_003CstreamingUrl_003E5__5 = _003CextractionTask_003E5__7.Result;
						_003Csuccess_003E5__6 = !string.IsNullOrEmpty(_003CstreamingUrl_003E5__5);
					}
					catch (Exception ex2)
					{
						Debug.LogError((object)("[YouTubeTV] Error getting task result: " + ex2.Message + "\n" + ex2.StackTrace));
					}
				}
				break;
			}
			if (_003Csuccess_003E5__6)
			{
				Debug.Log((object)("Streaming URL obtained for " + ((Object)((Component)youTubeTVController).gameObject).name + ": " + _003CstreamingUrl_003E5__5));
				youTubeTVController.PlayVideo(_003CstreamingUrl_003E5__5);
			}
			else
			{
				Debug.LogError((object)("Failed to get streaming URL for " + ((Object)((Component)youTubeTVController).gameObject).name));
				if ((Object)(object)youTubeTVController.tvScreenRenderer != (Object)null && (Object)(object)youTubeTVController.idlePlaceholderTexture != (Object)null)
				{
					youTubeTVController.tvScreenRenderer.material.SetTexture(youTubeTVController.tvScreenMaterialPropertyName, (Texture)(object)youTubeTVController.idlePlaceholderTexture);
				}
			}
			youTubeTVController.downloadCoroutine = null;
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CDownloadShortsCoroutine_003Ed__74 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CDownloadShortsCoroutine_003Ed__74(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Expected O, but got Unknown
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Expected O, but got Unknown
			int num = _003C_003E1__state;
			YouTubeTVController youTubeTVController = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				youTubeTVController.GetShortsPath();
				goto IL_00de;
			case 1:
				_003C_003E1__state = -1;
				goto IL_00de;
			case 2:
				_003C_003E1__state = -1;
				youTubeTVController.lastDownloadTime = Time.time;
				goto IL_00b8;
			case 3:
				{
					_003C_003E1__state = -1;
					goto IL_00de;
				}
				IL_00de:
				if (youTubeTVController.isPlayingShorts)
				{
					if (youTubeTVController.shortsPlaylist.Count < 3 && youTubeTVController.activeDownloads.Count < 1)
					{
						if (Time.time - youTubeTVController.lastDownloadTime < youTubeTVController.downloadRateLimit)
						{
							_003C_003E2__current = (object)new WaitForSeconds(1f);
							_003C_003E1__state = 1;
							return true;
						}
						_003C_003E2__current = ((MonoBehaviour)youTubeTVController).StartCoroutine(youTubeTVController.DownloadShortsSet());
						_003C_003E1__state = 2;
						return true;
					}
					goto IL_00b8;
				}
				return false;
				IL_00b8:
				youTubeTVController.CleanupOldShorts();
				_003C_003E2__current = (object)new WaitForSeconds(2f);
				_003C_003E1__state = 3;
				return true;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CDownloadShortsSet_003Ed__75 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController _003C_003E4__this;

		private _003C_003Ec__DisplayClass75_0 _003C_003E8__1;

		private string _003CshortsPath_003E5__2;

		private Task _003CdownloadTask_003E5__3;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CDownloadShortsSet_003Ed__75(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E8__1 = null;
			_003CshortsPath_003E5__2 = null;
			_003CdownloadTask_003E5__3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			YouTubeTVController youTubeTVController = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E8__1 = new _003C_003Ec__DisplayClass75_0();
				if (!YtDlpInitializer.IsInitialized)
				{
					Debug.LogError((object)"[YouTubeTV] Cannot download shorts - Python engine is not initialized. Check earlier log messages for initialization errors.");
					return false;
				}
				_003CshortsPath_003E5__2 = youTubeTVController.GetShortsPath();
				_003C_003E8__1.downloadId = Guid.NewGuid().ToString();
				youTubeTVController.activeDownloads.Add(_003C_003E8__1.downloadId);
				Debug.Log((object)("Starting Python yt-dlp shorts download: " + _003C_003E8__1.downloadId));
				_003C_003E8__1.capturedShortsPath = _003CshortsPath_003E5__2;
				_003C_003E8__1.capturedDownloadId = _003C_003E8__1.downloadId;
				_003CdownloadTask_003E5__3 = PythonTaskRunner.RunAsync(delegate
				{
					try
					{
						dynamic val = Py.Import("yt_dlp");
						dynamic val2 = Py.Import("builtins");
						PythonEngine.RunSimpleString("\r\ndef shorts_filter(info, *, incomplete):\r\n    duration = info.get('duration')\r\n    if duration and duration >= 60:\r\n        return 'Video is too long'\r\n");
						dynamic val3 = Py.Import("__main__");
						dynamic attr = val3.GetAttr("shorts_filter");
						dynamic val4 = val3.MyLogger();
						dynamic val5 = val.YoutubeDL(new PyDict
						{
							["format"] = new PyString("best[vcodec!=none][ext=mp4]/best[vcodec!=none][acodec!=none][ext=mp4]"),
							["outtmpl"] = new PyString(_003C_003E8__1.capturedShortsPath + "/" + _003C_003E8__1.capturedDownloadId + ".%(ext)s"),
							["restrictfilenames"] = val2.True,
							["nowritejson"] = val2.True,
							["noplaylist"] = val2.True,
							["max_downloads"] = new PyInt(3),
							["socket_timeout"] = new PyInt(15),
							["retries"] = new PyInt(1),
							["fragment_retries"] = new PyInt(1),
							["quiet"] = val2.True,
							["no_warnings"] = val2.True,
							["nocheckcertificate"] = val2.True,
							["cache_dir"] = val2.False,
							["geo_bypass"] = val2.True,
							["match_filter"] = attr,
							["playlistrandom"] = val2.True,
							["logger"] = val4
						});
						PyList pyList = new PyList();
						pyList.Append(new PyString("ytsearch10:shorts"));
						val5.download(pyList);
					}
					catch (Exception ex3)
					{
						Debug.LogError((object)("Python yt-dlp error in DownloadShortsSet: " + ex3.Message));
					}
				});
				break;
			case 1:
				_003C_003E1__state = -1;
				break;
			}
			if (!_003CdownloadTask_003E5__3.IsCompleted)
			{
				_003C_003E2__current = null;
				_003C_003E1__state = 1;
				return true;
			}
			if (_003CdownloadTask_003E5__3.IsFaulted)
			{
				Debug.LogError((object)$"DownloadShortsSet task failed: {_003CdownloadTask_003E5__3.Exception}");
			}
			else
			{
				Debug.Log((object)("Python yt-dlp shorts download finished: " + _003C_003E8__1.downloadId));
			}
			try
			{
				string[] array = (from f in Directory.GetFiles(_003CshortsPath_003E5__2, "*.mp4")
					where f.Contains(_003C_003E8__1.downloadId)
					select f).ToArray();
				Debug.Log((object)$"Found {array.Length} video files for download {_003C_003E8__1.downloadId}");
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (File.Exists(text) && new FileInfo(text).Length > 1024)
					{
						if (youTubeTVController.IsDuplicateVideo(text))
						{
							Debug.Log((object)("Skipping duplicate video: " + Path.GetFileName(text)));
							try
							{
								File.Delete(text);
							}
							catch (Exception ex)
							{
								Debug.LogWarning((object)("Failed to delete duplicate file: " + ex.Message));
							}
						}
						else
						{
							youTubeTVController.shortsPlaylist.Add(text);
							Debug.Log((object)("Added shorts video to playlist: " + Path.GetFileName(text)));
						}
					}
					else
					{
						Debug.LogWarning((object)("Skipping invalid or empty file: " + Path.GetFileName(text)));
					}
				}
				if (array.Length == 0)
				{
					Debug.LogWarning((object)("No video files found for download " + _003C_003E8__1.downloadId));
				}
			}
			catch (Exception ex2)
			{
				Debug.LogError((object)("Error processing downloaded shorts: " + ex2.Message));
			}
			youTubeTVController.activeDownloads.Remove(_003C_003E8__1.downloadId);
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CInitializePythonAndSetup_003Ed__56 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController _003C_003E4__this;

		private float _003CwaitTime_003E5__2;

		private float _003CmaxWaitTime_003E5__3;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CInitializePythonAndSetup_003Ed__56(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Expected O, but got Unknown
			int num = _003C_003E1__state;
			YouTubeTVController youTubeTVController = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				if (!EnsurePythonRuntimeLoaded())
				{
					return false;
				}
				_003C_003E2__current = null;
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				if (!YtDlpInitializer.IsInitialized && !YtDlpInitializer.InitializationFailed)
				{
					YtDlpInitializer.Initialize();
				}
				_003C_003E2__current = null;
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				_003CwaitTime_003E5__2 = 0f;
				_003CmaxWaitTime_003E5__3 = 15f;
				break;
			case 3:
				_003C_003E1__state = -1;
				_003CwaitTime_003E5__2 += 0.5f;
				break;
			}
			if (!PythonTaskRunner.IsReady && YtDlpInitializer.IsInitialized && _003CwaitTime_003E5__2 < _003CmaxWaitTime_003E5__3)
			{
				_003C_003E2__current = (object)new WaitForSeconds(0.5f);
				_003C_003E1__state = 3;
				return true;
			}
			if (youTubeTVController.isInitialized && youTubeTVController.autoPlayOnStart && !string.IsNullOrEmpty(youTubeTVController.defaultYouTubeURL) && (Object)(object)((Component)youTubeTVController).GetComponent<YouTubeTVInitializer>() == (Object)null)
			{
				youTubeTVController.PlayYouTubeVideo(youTubeTVController.defaultYouTubeURL);
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CLoadPlaceholderTextures_003Ed__52 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CLoadPlaceholderTextures_003Ed__52(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			YouTubeTVController CS_0024_003C_003E8__locals0 = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				if ((Object)(object)CS_0024_003C_003E8__locals0.tvScreenRenderer != (Object)null && (Object)(object)CS_0024_003C_003E8__locals0.blackTexture != (Object)null)
				{
					CS_0024_003C_003E8__locals0.tvScreenRenderer.material.SetTexture(CS_0024_003C_003E8__locals0.tvScreenMaterialPropertyName, (Texture)(object)CS_0024_003C_003E8__locals0.blackTexture);
				}
				_003C_003E2__current = ((MonoBehaviour)CS_0024_003C_003E8__locals0).StartCoroutine(CS_0024_003C_003E8__locals0.LoadTextureFromFile(CS_0024_003C_003E8__locals0.idlePlaceholderResourcePath, delegate(Texture2D texture)
				{
					CS_0024_003C_003E8__locals0.idlePlaceholderTexture = texture;
				}));
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = ((MonoBehaviour)CS_0024_003C_003E8__locals0).StartCoroutine(CS_0024_003C_003E8__locals0.LoadTextureFromFile(CS_0024_003C_003E8__locals0.loadingPlaceholderResourcePath, delegate(Texture2D texture)
				{
					CS_0024_003C_003E8__locals0.loadingPlaceholderTexture = texture;
				}));
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				CS_0024_003C_003E8__locals0.isInitialized = true;
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CLoadTextureFromFile_003Ed__53 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public string filePath;

		public Action<Texture2D> callback;

		private UnityWebRequest _003Crequest_003E5__2;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CLoadTextureFromFile_003Ed__53(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || num == 1)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003Crequest_003E5__2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Invalid comparison between Unknown and I4
			try
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
				{
					_003C_003E1__state = -1;
					string text = "file://" + filePath;
					_003Crequest_003E5__2 = UnityWebRequestTexture.GetTexture(text);
					_003C_003E1__state = -3;
					_003C_003E2__current = _003Crequest_003E5__2.SendWebRequest();
					_003C_003E1__state = 1;
					return true;
				}
				case 1:
					_003C_003E1__state = -3;
					if ((int)_003Crequest_003E5__2.result == 1)
					{
						Texture2D content = DownloadHandlerTexture.GetContent(_003Crequest_003E5__2);
						callback(content);
					}
					else
					{
						callback(null);
					}
					_003C_003Em__Finally1();
					_003Crequest_003E5__2 = null;
					return false;
				}
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003Crequest_003E5__2 != null)
			{
				((IDisposable)_003Crequest_003E5__2).Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CPlayShortVideo_003Ed__77 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController _003C_003E4__this;

		public string videoPath;

		private float _003CprepareTimeout_003E5__2;

		private float _003Celapsed_003E5__3;

		private float _003CmaxDuration_003E5__4;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CPlayShortVideo_003Ed__77(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Expected O, but got Unknown
			//IL_0275: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Expected O, but got Unknown
			int num = _003C_003E1__state;
			YouTubeTVController youTubeTVController = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				youTubeTVController.currentVideoPath = videoPath;
				bool flag = false;
				try
				{
					string text = "file:///" + videoPath.Replace("\\", "/");
					Debug.Log((object)("Setting video URL: " + text));
					if (youTubeTVController.videoPlayer.isPlaying)
					{
						youTubeTVController.videoPlayer.Stop();
					}
					youTubeTVController.ConfigureVideoPlayerForShorts();
					youTubeTVController.videoPlayer.source = (VideoSource)1;
					youTubeTVController.videoPlayer.url = text;
					youTubeTVController.videoPlayer.Prepare();
					Debug.Log((object)("Video preparation started for: " + Path.GetFileName(videoPath)));
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("Error preparing short video " + Path.GetFileName(videoPath) + ": " + ex.Message));
					flag = true;
				}
				if (flag)
				{
					youTubeTVController.currentVideoPath = null;
					return false;
				}
				_003CprepareTimeout_003E5__2 = 10f;
				_003Celapsed_003E5__3 = 0f;
				goto IL_0149;
			}
			case 1:
				_003C_003E1__state = -1;
				_003Celapsed_003E5__3 += 0.2f;
				goto IL_0149;
			case 2:
				{
					_003C_003E1__state = -1;
					_003Celapsed_003E5__3 += 0.5f;
					break;
				}
				IL_0149:
				if (!youTubeTVController.videoPlayer.isPrepared && youTubeTVController.isPlayingShorts && _003Celapsed_003E5__3 < _003CprepareTimeout_003E5__2)
				{
					_003C_003E2__current = (object)new WaitForSeconds(0.2f);
					_003C_003E1__state = 1;
					return true;
				}
				if (!youTubeTVController.isPlayingShorts)
				{
					youTubeTVController.currentVideoPath = null;
					return false;
				}
				if (!youTubeTVController.videoPlayer.isPrepared)
				{
					Debug.LogError((object)("Video preparation timed out for: " + Path.GetFileName(videoPath)));
					youTubeTVController.currentVideoPath = null;
					return false;
				}
				if ((Object)(object)youTubeTVController.tvScreenRenderer != (Object)null && (Object)(object)youTubeTVController.videoRenderTexture != (Object)null && youTubeTVController.videoRenderTexture.IsCreated())
				{
					youTubeTVController.tvScreenRenderer.material.SetTexture(youTubeTVController.tvScreenMaterialPropertyName, (Texture)(object)youTubeTVController.videoRenderTexture);
				}
				try
				{
					youTubeTVController.videoPlayer.Play();
					youTubeTVController.isPlaying = true;
					Debug.Log((object)("Started playing short: " + Path.GetFileName(videoPath)));
				}
				catch (Exception ex2)
				{
					Debug.LogError((object)("Error starting short video playback " + Path.GetFileName(videoPath) + ": " + ex2.Message));
					youTubeTVController.currentVideoPath = null;
					return false;
				}
				_003CmaxDuration_003E5__4 = 65f;
				_003Celapsed_003E5__3 = 0f;
				break;
			}
			if (youTubeTVController.videoPlayer.isPlaying && youTubeTVController.isPlayingShorts && _003Celapsed_003E5__3 < _003CmaxDuration_003E5__4)
			{
				_003C_003E2__current = (object)new WaitForSeconds(0.5f);
				_003C_003E1__state = 2;
				return true;
			}
			if (_003Celapsed_003E5__3 >= _003CmaxDuration_003E5__4)
			{
				Debug.LogWarning((object)("Short video exceeded maximum duration, stopping: " + Path.GetFileName(videoPath)));
			}
			try
			{
				if (youTubeTVController.videoPlayer.isPlaying)
				{
					youTubeTVController.videoPlayer.Stop();
				}
				youTubeTVController.isPlaying = false;
				Debug.Log((object)("Finished playing short: " + Path.GetFileName(videoPath)));
				if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
				{
					try
					{
						File.Delete(videoPath);
						Debug.Log((object)("Deleted played short: " + Path.GetFileName(videoPath)));
					}
					catch (Exception ex3)
					{
						Debug.LogError((object)("Error deleting short video " + Path.GetFileName(videoPath) + ": " + ex3.Message));
					}
				}
			}
			catch (Exception ex4)
			{
				Debug.LogError((object)("Error stopping short video " + Path.GetFileName(videoPath) + ": " + ex4.Message));
				youTubeTVController.isPlaying = false;
			}
			youTubeTVController.currentVideoPath = null;
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CShortsPlaybackCoroutine_003Ed__76 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController _003C_003E4__this;

		private int _003CwaitCount_003E5__2;

		private string _003CshortPath_003E5__3;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CShortsPlaybackCoroutine_003Ed__76(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003CshortPath_003E5__3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Expected O, but got Unknown
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Expected O, but got Unknown
			int num = _003C_003E1__state;
			YouTubeTVController youTubeTVController = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003CwaitCount_003E5__2 = 0;
				Debug.Log((object)"Starting shorts playback coroutine");
				goto IL_02e8;
			case 1:
				_003C_003E1__state = -1;
				_003CwaitCount_003E5__2++;
				goto IL_0096;
			case 2:
				_003C_003E1__state = -1;
				try
				{
					if (File.Exists(_003CshortPath_003E5__3))
					{
						_ = new FileInfo(_003CshortPath_003E5__3).Length;
						youTubeTVController.playedVideoPaths.Add(Path.GetFullPath(_003CshortPath_003E5__3));
						Debug.Log((object)("Marked video as played: " + Path.GetFileName(_003CshortPath_003E5__3)));
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("Failed to track played video: " + ex.Message));
				}
				youTubeTVController.currentShortsIndex++;
				if (youTubeTVController.currentShortsIndex >= youTubeTVController.shortsPlaylist.Count)
				{
					Debug.Log((object)"Reached end of shorts playlist, shuffling and starting over");
					youTubeTVController.ShufflePlaylist();
					youTubeTVController.currentShortsIndex = 0;
					if (youTubeTVController.playedVideoPaths.Count > youTubeTVController.shortsPlaylist.Count * 2)
					{
						youTubeTVController.playedVideoPaths.Clear();
						Debug.Log((object)"Cleared played videos tracking to allow replaying");
					}
				}
				_003CshortPath_003E5__3 = null;
				goto IL_02c8;
			case 3:
				{
					_003C_003E1__state = -1;
					goto IL_02e8;
				}
				IL_02e8:
				if (!youTubeTVController.isPlayingShorts)
				{
					break;
				}
				goto IL_0096;
				IL_02c8:
				_003C_003E2__current = (object)new WaitForSeconds(0.5f);
				_003C_003E1__state = 3;
				return true;
				IL_0096:
				if (youTubeTVController.shortsPlaylist.Count == 0 && youTubeTVController.isPlayingShorts && _003CwaitCount_003E5__2 < 15)
				{
					Debug.Log((object)$"Waiting for shorts to download... ({_003CwaitCount_003E5__2 + 1}/{15})");
					_003C_003E2__current = (object)new WaitForSeconds(1f);
					_003C_003E1__state = 1;
					return true;
				}
				if (!youTubeTVController.isPlayingShorts)
				{
					break;
				}
				if (youTubeTVController.shortsPlaylist.Count == 0)
				{
					Debug.LogError((object)"No shorts available after waiting. Stopping shorts playback.");
					youTubeTVController.isPlayingShorts = false;
					break;
				}
				Debug.Log((object)$"Shorts playlist has {youTubeTVController.shortsPlaylist.Count} videos, current index: {youTubeTVController.currentShortsIndex}");
				if (youTubeTVController.currentShortsIndex >= youTubeTVController.shortsPlaylist.Count)
				{
					goto IL_02c8;
				}
				_003CshortPath_003E5__3 = youTubeTVController.shortsPlaylist[youTubeTVController.currentShortsIndex];
				if (File.Exists(_003CshortPath_003E5__3))
				{
					Debug.Log((object)$"Playing short {youTubeTVController.currentShortsIndex + 1}/{youTubeTVController.shortsPlaylist.Count}: {Path.GetFileName(_003CshortPath_003E5__3)}");
					_003C_003E2__current = ((MonoBehaviour)youTubeTVController).StartCoroutine(youTubeTVController.PlayShortVideo(_003CshortPath_003E5__3));
					_003C_003E1__state = 2;
					return true;
				}
				Debug.LogWarning((object)("Short file not found: " + _003CshortPath_003E5__3));
				youTubeTVController.shortsPlaylist.RemoveAt(youTubeTVController.currentShortsIndex);
				if (youTubeTVController.currentShortsIndex >= youTubeTVController.shortsPlaylist.Count)
				{
					youTubeTVController.currentShortsIndex = 0;
				}
				goto IL_02e8;
			}
			Debug.Log((object)"Shorts playback coroutine ended");
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[Header("TV Settings")]
	[SerializeField]
	private Renderer tvScreenRenderer;

	[SerializeField]
	private string tvScreenMaterialPropertyName = "_MainTex";

	private Texture2D idlePlaceholderTexture;

	private Texture2D loadingPlaceholderTexture;

	private Texture2D blackTexture;

	[Header("Default Video")]
	[SerializeField]
	private string defaultYouTubeURL = "https://www.youtube.com/watch?v=P-rneb_yxUs";

	[SerializeField]
	private bool autoPlayOnStart = true;

	[SerializeField]
	private bool loopVideo = true;

	[Header("Video Settings")]
	[SerializeField]
	private RenderTexture videoRenderTexture;

	[SerializeField]
	private Vector2 renderTextureSize = new Vector2(1920f, 1080f);

	[Header("yt-dlp Settings")]
	[SerializeField]
	private string downloadFolder = "Downloads";

	[SerializeField]
	private string shortsFolder = "shorts_temp";

	[Header("Shorts Settings")]
	[SerializeField]
	private bool enableShorts;

	[SerializeField]
	private int maxConcurrentDownloads = 2;

	[SerializeField]
	private int shortsPlaylistSize = 10;

	[SerializeField]
	private float downloadRateLimit = 5f;

	[SerializeField]
	private int maxCachedShorts = 20;

	public VideoPlayer videoPlayer;

	private AudioSource audioSource;

	private bool isPlaying;

	private string currentVideoPath;

	private Coroutine downloadCoroutine;

	private bool isInitialized;

	private bool isPlayingShorts;

	private List<string> shortsPlaylist = new List<string>();

	private int currentShortsIndex;

	private Coroutine shortsDownloadCoroutine;

	private Coroutine shortsPlaybackCoroutine;

	private Queue<string> downloadQueue = new Queue<string>();

	private HashSet<string> activeDownloads = new HashSet<string>();

	private float lastDownloadTime;

	private bool shortsCleanupRequested;

	private double pendingStartTime;

	private HashSet<string> playedVideoPaths = new HashSet<string>();

	private string tvInstanceId;

	private StringBuilder ytDlpOutput;

	private StringBuilder ytDlpError;

	private YTConfig _ytConfig;

	private float _volume = 0.5f;

	private string idlePlaceholderResourcePath => GetResourcePath("youtubelogo.jpg");

	private string loadingPlaceholderResourcePath => GetResourcePath("youtubeloading.jpg");

	public TileEntityYouTubeTV ParentTileEntity { get; set; }

	private string GetResourcePath(string filename)
	{
		string fullName = Directory.GetParent(Application.dataPath).FullName;
		return Path.Combine(fullName, "Mods", "YoutubeTVMod", "Resources", filename);
	}

	public void Initialize(Renderer screenRenderer, bool forceReinit = false)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected O, but got Unknown
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		tvScreenRenderer = screenRenderer;
		autoPlayOnStart = false;
		if ((Object)(object)GameManager.Instance == (Object)null || GameManager.Instance.World == null)
		{
			((Behaviour)this).enabled = false;
			return;
		}
		((Behaviour)this).enabled = true;
		if (forceReinit || !isInitialized)
		{
			videoPlayer = ((Component)this).GetComponent<VideoPlayer>();
			audioSource = ((Component)this).GetComponent<AudioSource>();
			if ((Object)(object)videoPlayer == (Object)null)
			{
				videoPlayer = ((Component)this).gameObject.AddComponent<VideoPlayer>();
			}
			if ((Object)(object)audioSource == (Object)null)
			{
				audioSource = ((Component)this).gameObject.AddComponent<AudioSource>();
			}
			if ((Object)(object)videoPlayer != (Object)null)
			{
				videoPlayer.Stop();
				videoPlayer.prepareCompleted -= new VideoPlayer.EventHandler(OnVideoPrepared);
				videoPlayer.loopPointReached -= new VideoPlayer.EventHandler(OnVideoEnd);
				videoPlayer.clip = null;
				videoPlayer.source = (VideoSource)0;
				videoPlayer.url = null;
			}
			if (forceReinit && (Object)(object)videoRenderTexture != (Object)null)
			{
				if (videoRenderTexture.IsCreated())
				{
					videoRenderTexture.Release();
				}
				videoRenderTexture = null;
			}
			Awake();
		}
		else if ((Object)(object)tvScreenRenderer != (Object)null)
		{
			if ((Object)(object)videoPlayer != (Object)null && videoPlayer.isPlaying && (Object)(object)videoRenderTexture != (Object)null && videoRenderTexture.IsCreated())
			{
				tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
			}
			else if (downloadCoroutine != null && (Object)(object)loadingPlaceholderTexture != (Object)null)
			{
				tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)loadingPlaceholderTexture);
			}
			else if ((Object)(object)idlePlaceholderTexture != (Object)null)
			{
				tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)idlePlaceholderTexture);
			}
			else if ((Object)(object)videoRenderTexture != (Object)null && videoRenderTexture.IsCreated())
			{
				tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
			}
		}
	}

	public void Awake()
	{
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Expected O, but got Unknown
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Expected O, but got Unknown
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)GameManager.Instance == (Object)null || GameManager.Instance.World == null)
		{
			((Behaviour)this).enabled = false;
			return;
		}
		tvInstanceId = Guid.NewGuid().ToString();
		videoPlayer = ((Component)this).GetComponent<VideoPlayer>();
		audioSource = ((Component)this).GetComponent<AudioSource>();
		if ((Object)(object)videoPlayer == (Object)null)
		{
			videoPlayer = ((Component)this).gameObject.AddComponent<VideoPlayer>();
		}
		if ((Object)(object)audioSource == (Object)null)
		{
			audioSource = ((Component)this).gameObject.AddComponent<AudioSource>();
		}
		if ((Object)(object)videoPlayer == (Object)null || (Object)(object)audioSource == (Object)null)
		{
			((Behaviour)this).enabled = false;
			return;
		}
		videoPlayer.playOnAwake = false;
		videoPlayer.renderMode = (VideoRenderMode)2;
		videoPlayer.audioOutputMode = (VideoAudioOutputMode)1;
		videoPlayer.SetTargetAudioSource((ushort)0, audioSource);
		videoPlayer.aspectRatio = (VideoAspectRatio)1;
		audioSource.spatialBlend = 1f;
		audioSource.rolloffMode = (AudioRolloffMode)1;
		audioSource.minDistance = 1f;
		audioSource.maxDistance = 20f;
		audioSource.dopplerLevel = 0f;
		audioSource.volume = _volume;
		if ((Object)(object)videoRenderTexture != (Object)null)
		{
			if (videoRenderTexture.IsCreated())
			{
				videoRenderTexture.Release();
			}
			Object.Destroy((Object)(object)videoRenderTexture);
			videoRenderTexture = null;
		}
		videoRenderTexture = new RenderTexture((int)renderTextureSize.x, (int)renderTextureSize.y, 0, (RenderTextureFormat)0);
		((Object)videoRenderTexture).name = $"YouTubeTV_RT_{((Object)((Component)this).gameObject).GetInstanceID()}";
		if (!videoRenderTexture.Create())
		{
			((Behaviour)this).enabled = false;
			return;
		}
		videoPlayer.targetTexture = videoRenderTexture;
		string path = Path.Combine(Application.persistentDataPath, downloadFolder);
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
		string shortsPath = GetShortsPath();
		if (!Directory.Exists(shortsPath))
		{
			Directory.CreateDirectory(shortsPath);
		}
		blackTexture = new Texture2D(1, 1);
		blackTexture.SetPixel(0, 0, Color.black);
		blackTexture.Apply();
		((MonoBehaviour)this).StartCoroutine(LoadPlaceholderTextures());
		isInitialized = true;
	}

	[IteratorStateMachine(typeof(_003CLoadPlaceholderTextures_003Ed__52))]
	private IEnumerator LoadPlaceholderTextures()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CLoadPlaceholderTextures_003Ed__52(0)
		{
			_003C_003E4__this = this
		};
	}

	[IteratorStateMachine(typeof(_003CLoadTextureFromFile_003Ed__53))]
	private IEnumerator LoadTextureFromFile(string filePath, Action<Texture2D> callback)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CLoadTextureFromFile_003Ed__53(0)
		{
			filePath = filePath,
			callback = callback
		};
	}

	private void Start()
	{
		if (((Behaviour)this).enabled)
		{
			((MonoBehaviour)this).StartCoroutine(InitializePythonAndSetup());
		}
	}

	private static bool EnsurePythonRuntimeLoaded()
	{
		return !string.IsNullOrEmpty(FindExternalYtDlpExecutable());
	}

	[IteratorStateMachine(typeof(_003CInitializePythonAndSetup_003Ed__56))]
	private IEnumerator InitializePythonAndSetup()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CInitializePythonAndSetup_003Ed__56(0)
		{
			_003C_003E4__this = this
		};
	}

	public void SetYTConfig(YTConfig config)
	{
		_ytConfig = config;
	}

	public void PlayYouTubeVideo(string youtubeURL)
	{
		if (!isInitialized || string.IsNullOrEmpty(youtubeURL) || !IsValidYouTubeURL(youtubeURL))
		{
			return;
		}
		StopAllPlayback();
		if (IsYouTubeShortsURL(youtubeURL))
		{
			StartShortsPlaylist();
			return;
		}
		if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)loadingPlaceholderTexture != (Object)null)
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)loadingPlaceholderTexture);
		}
		if (!((Behaviour)this).enabled || !((Component)this).gameObject.activeInHierarchy)
		{
			if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)idlePlaceholderTexture != (Object)null)
			{
				tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)idlePlaceholderTexture);
			}
		}
		else
		{
			downloadCoroutine = ((MonoBehaviour)this).StartCoroutine(DownloadAndPlayVideo(youtubeURL));
		}
	}

	public void StopAllPlayback()
	{
		if (downloadCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(downloadCoroutine);
			downloadCoroutine = null;
		}
		if (shortsPlaybackCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(shortsPlaybackCoroutine);
			shortsPlaybackCoroutine = null;
		}
		if (shortsDownloadCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(shortsDownloadCoroutine);
			shortsDownloadCoroutine = null;
		}
		isPlayingShorts = false;
		currentShortsIndex = 0;
		downloadQueue.Clear();
		activeDownloads.Clear();
		if ((Object)(object)videoPlayer != (Object)null && videoPlayer.isPlaying)
		{
			videoPlayer.Stop();
		}
		isPlaying = false;
	}

	private bool IsValidYouTubeURL(string url)
	{
		if (!url.Contains("youtube.com/watch") && !url.Contains("youtu.be/"))
		{
			return url.ToLower().Contains("shorts");
		}
		return true;
	}

	private bool IsYouTubeShortsURL(string url)
	{
		if (!url.ToLower().Contains("shorts"))
		{
			return url.ToLower() == "shorts";
		}
		return true;
	}

	private string GetShortsPath()
	{
		return Path.Combine(GameIO.GetGamePath(), "Mods", "YoutubeTVMod", shortsFolder);
	}

	public Vector3i GetBlockPosition()
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (ParentTileEntity != null)
		{
			return ((TileEntity)ParentTileEntity).ToWorldPos();
		}
		Log.Warning("[YouTubeTVMod] YouTubeTVController on " + ((Object)((Component)this).gameObject).name + " does not have ParentTileEntity set. Cannot get block position.");
		return Vector3i.zero;
	}

	public double GetCurrentVideoTime()
	{
		if ((Object)(object)videoPlayer != (Object)null)
		{
			return videoPlayer.time;
		}
		return 0.0;
	}

	[IteratorStateMachine(typeof(_003CDownloadAndPlayVideo_003Ed__65))]
	private IEnumerator DownloadAndPlayVideo(string youtubeURL)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CDownloadAndPlayVideo_003Ed__65(0)
		{
			_003C_003E4__this = this,
			youtubeURL = youtubeURL
		};
	}

	private void PlayVideo(string videoPath)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected O, but got Unknown
		currentVideoPath = videoPath;
		ConfigureVideoPlayerForRegular();
		if (videoPath.StartsWith("http://") || videoPath.StartsWith("https://"))
		{
			videoPlayer.url = videoPath;
		}
		else
		{
			videoPlayer.url = "file:///" + videoPath.Replace("\\", "/");
		}
		videoPlayer.prepareCompleted -= new VideoPlayer.EventHandler(OnVideoPrepared);
		videoPlayer.loopPointReached -= new VideoPlayer.EventHandler(OnVideoEnd);
		videoPlayer.prepareCompleted += new VideoPlayer.EventHandler(OnVideoPrepared);
		videoPlayer.loopPointReached += new VideoPlayer.EventHandler(OnVideoEnd);
		videoPlayer.Prepare();
	}

	private void OnVideoPrepared(VideoPlayer vp)
	{
		if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)videoRenderTexture != (Object)null && videoRenderTexture.IsCreated())
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
			Debug.Log((object)("YouTubeTVController.OnVideoPrepared: Set screen to VIDEO content for " + ((Object)((Component)this).gameObject).name + "."));
		}
		else
		{
			Debug.LogWarning((object)("YouTubeTVController.OnVideoPrepared: tvScreenRenderer or videoRenderTexture is null for " + ((Object)((Component)this).gameObject).name + ". Cannot set screen to video."));
		}
		if (pendingStartTime > 0.0 && vp.canSetTime)
		{
			Debug.Log((object)$"YouTubeTVController.OnVideoPrepared: Applying pending start time: {pendingStartTime} for {((Object)((Component)this).gameObject).name}");
			vp.time = pendingStartTime;
			pendingStartTime = 0.0;
		}
		else if (pendingStartTime > 0.0 && !vp.canSetTime)
		{
			Debug.LogWarning((object)$"YouTubeTVController.OnVideoPrepared: Had pending start time {pendingStartTime} but VideoPlayer cannot set time for {((Object)((Component)this).gameObject).name}. Video will start from beginning.");
			pendingStartTime = 0.0;
		}
		vp.Play();
		isPlaying = true;
		Debug.Log((object)$"Video playback started for {((Object)((Component)this).gameObject).name} at time {vp.time}");
		downloadCoroutine = null;
	}

	private void OnVideoEnd(VideoPlayer vp)
	{
		if (!isPlayingShorts)
		{
			if (loopVideo)
			{
				videoPlayer.Play();
			}
			else
			{
				StopVideo();
			}
		}
	}

	public void StopVideo()
	{
		StopAllPlayback();
		if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)idlePlaceholderTexture != (Object)null)
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)idlePlaceholderTexture);
			Debug.Log((object)("YouTubeTVController.StopVideo/OnVideoEnd: Set screen to IDLE placeholder: " + idlePlaceholderResourcePath + " for " + ((Object)((Component)this).gameObject).name));
		}
		else if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)videoRenderTexture != (Object)null)
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
			Debug.LogWarning((object)("YouTubeTVController.StopVideo/OnVideoEnd: IDLE placeholder not loaded for " + ((Object)((Component)this).gameObject).name + ". Screen set to videoRenderTexture."));
		}
		if (!string.IsNullOrEmpty(currentVideoPath) && !currentVideoPath.StartsWith("http://") && !currentVideoPath.StartsWith("https://") && File.Exists(currentVideoPath))
		{
			try
			{
				File.Delete(currentVideoPath);
			}
			catch (Exception ex)
			{
				Debug.LogWarning((object)("Failed to delete video file: " + ex.Message));
			}
		}
		currentVideoPath = null;
		if (shortsCleanupRequested)
		{
			CleanupShortsFiles();
			shortsCleanupRequested = false;
		}
	}

	public void SetScreenBlack(bool isBlack)
	{
		if ((Object)(object)tvScreenRenderer == (Object)null)
		{
			Debug.LogWarning((object)("YouTubeTVController.SetScreenBlack: tvScreenRenderer is null for " + ((Object)((Component)this).gameObject).name + ". Cannot set screen black."));
		}
		else if (isBlack)
		{
			if ((Object)(object)blackTexture != (Object)null)
			{
				tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)blackTexture);
				Debug.Log((object)("YouTubeTVController.SetScreenBlack: Set screen to BLACK for " + ((Object)((Component)this).gameObject).name + "."));
			}
			else
			{
				Debug.LogError((object)("YouTubeTVController.SetScreenBlack: Black texture is null for " + ((Object)((Component)this).gameObject).name + ". Cannot set screen black."));
			}
		}
		else if (isPlaying && (Object)(object)videoRenderTexture != (Object)null && videoRenderTexture.IsCreated())
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
			Debug.Log((object)("YouTubeTVController.SetScreenBlack: Restored screen to VIDEO for " + ((Object)((Component)this).gameObject).name + "."));
		}
		else if ((Object)(object)idlePlaceholderTexture != (Object)null)
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)idlePlaceholderTexture);
			Debug.Log((object)("YouTubeTVController.SetScreenBlack: Restored screen to IDLE (YouTube Logo) for " + ((Object)((Component)this).gameObject).name + "."));
		}
		else if ((Object)(object)videoRenderTexture != (Object)null && videoRenderTexture.IsCreated())
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
			Debug.LogWarning((object)("YouTubeTVController.SetScreenBlack: IDLE placeholder not loaded for " + ((Object)((Component)this).gameObject).name + ". Screen set to videoRenderTexture as fallback."));
		}
	}

	public void DisplayYouTubeLogo()
	{
		if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)idlePlaceholderTexture != (Object)null)
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)idlePlaceholderTexture);
			Debug.Log((object)("YouTubeTVController.DisplayYouTubeLogo: Set screen to YouTube Logo for " + ((Object)((Component)this).gameObject).name + "."));
		}
		else
		{
			Debug.LogWarning((object)("YouTubeTVController.DisplayYouTubeLogo: idlePlaceholderTexture is null or tvScreenRenderer is null for " + ((Object)((Component)this).gameObject).name + ". Cannot display logo."));
		}
	}

	public void SetShortsEnabled(bool enabled)
	{
		enableShorts = enabled;
		Debug.Log((object)("YouTubeTVController: Shorts functionality " + (enabled ? "enabled" : "disabled") + " for " + ((Object)((Component)this).gameObject).name));
	}

	private void StartShortsPlaylist()
	{
		if (!enableShorts)
		{
			Debug.LogWarning((object)"YouTube Shorts is disabled");
			return;
		}
		Debug.Log((object)"Starting YouTube Shorts playlist");
		isPlayingShorts = true;
		shortsPlaylist.Clear();
		currentShortsIndex = 0;
		if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)loadingPlaceholderTexture != (Object)null)
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)loadingPlaceholderTexture);
		}
		if (shortsDownloadCoroutine == null)
		{
			shortsDownloadCoroutine = ((MonoBehaviour)this).StartCoroutine(DownloadShortsCoroutine());
		}
		if (shortsPlaybackCoroutine == null)
		{
			shortsPlaybackCoroutine = ((MonoBehaviour)this).StartCoroutine(ShortsPlaybackCoroutine());
		}
	}

	[IteratorStateMachine(typeof(_003CDownloadShortsCoroutine_003Ed__74))]
	private IEnumerator DownloadShortsCoroutine()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CDownloadShortsCoroutine_003Ed__74(0)
		{
			_003C_003E4__this = this
		};
	}

	[IteratorStateMachine(typeof(_003CDownloadShortsSet_003Ed__75))]
	private IEnumerator DownloadShortsSet()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CDownloadShortsSet_003Ed__75(0)
		{
			_003C_003E4__this = this
		};
	}

	[IteratorStateMachine(typeof(_003CShortsPlaybackCoroutine_003Ed__76))]
	private IEnumerator ShortsPlaybackCoroutine()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CShortsPlaybackCoroutine_003Ed__76(0)
		{
			_003C_003E4__this = this
		};
	}

	[IteratorStateMachine(typeof(_003CPlayShortVideo_003Ed__77))]
	private IEnumerator PlayShortVideo(string videoPath)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CPlayShortVideo_003Ed__77(0)
		{
			_003C_003E4__this = this,
			videoPath = videoPath
		};
	}

	private void CleanupOldShorts()
	{
		try
		{
			string shortsPath = GetShortsPath();
			if (!Directory.Exists(shortsPath))
			{
				return;
			}
			List<FileInfo> list = (from f in Directory.GetFiles(shortsPath, "*.mp4")
				select new FileInfo(f) into f
				orderby f.LastWriteTime
				select f).ToList();
			while (list.Count > maxCachedShorts)
			{
				FileInfo fileInfo = list[0];
				try
				{
					if (!shortsPlaylist.Contains(fileInfo.FullName))
					{
						fileInfo.Delete();
						Debug.Log((object)("Cleaned up old short: " + fileInfo.Name));
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("Failed to delete old short " + fileInfo.Name + ": " + ex.Message));
				}
				list.RemoveAt(0);
			}
		}
		catch (Exception ex2)
		{
			Debug.LogError((object)("Error during shorts cleanup: " + ex2.Message));
		}
	}

	private void CleanupShortsFiles()
	{
		try
		{
			string shortsPath = GetShortsPath();
			if (!Directory.Exists(shortsPath))
			{
				return;
			}
			Debug.Log((object)"Cleaning up all shorts files");
			foreach (string item in (from f in Directory.GetFiles(shortsPath, "*.*")
				where f.EndsWith(".mp4") || f.EndsWith(".info.json") || f.EndsWith(".part")
				select f).ToList())
			{
				try
				{
					if (item != currentVideoPath)
					{
						File.Delete(item);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning((object)("Failed to delete shorts file " + Path.GetFileName(item) + ": " + ex.Message));
				}
			}
		}
		catch (Exception ex2)
		{
			Debug.LogError((object)("Error during shorts files cleanup: " + ex2.Message));
		}
	}

	private bool IsDuplicateVideo(string videoPath)
	{
		try
		{
			if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
			{
				return false;
			}
			string fullPath = Path.GetFullPath(videoPath);
			if (playedVideoPaths.Contains(fullPath))
			{
				Debug.Log((object)("Video already played: " + Path.GetFileName(videoPath)));
				return true;
			}
			foreach (string item in shortsPlaylist)
			{
				if (!string.IsNullOrEmpty(item) && Path.GetFullPath(item) == fullPath)
				{
					Debug.Log((object)("Video already in playlist: " + Path.GetFileName(videoPath)));
					return true;
				}
			}
			return false;
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("Error checking for duplicate video '" + videoPath + "': " + ex.Message));
			return false;
		}
	}

	private void ShufflePlaylist()
	{
		if (shortsPlaylist.Count > 1)
		{
			Debug.Log((object)$"Shuffling playlist of {shortsPlaylist.Count} shorts");
			for (int num = shortsPlaylist.Count - 1; num > 0; num--)
			{
				int index = UnityEngine.Random.Range(0, num + 1);
				string value = shortsPlaylist[num];
				shortsPlaylist[num] = shortsPlaylist[index];
				shortsPlaylist[index] = value;
			}
			Debug.Log((object)"Playlist shuffled successfully");
		}
	}

	private void ConfigureVideoPlayerForShorts()
	{
		if (!((Object)(object)videoPlayer == (Object)null))
		{
			videoPlayer.aspectRatio = (VideoAspectRatio)3;
			Debug.Log((object)("Configured video player for shorts with FitInside aspect ratio for " + ((Object)((Component)this).gameObject).name));
		}
	}

	public void AdjustVolume(float delta)
	{
		_volume = Mathf.Clamp01(_volume + delta);
		if ((Object)(object)audioSource != (Object)null)
		{
			audioSource.volume = _volume;
		}
	}

	public void SetVolume(float volumeLevel)
	{
		_volume = Mathf.Clamp01(volumeLevel);
		if ((Object)(object)audioSource != (Object)null)
		{
			audioSource.volume = _volume;
		}
	}

	public float GetCurrentVolume()
	{
		return _volume;
	}

	private void ConfigureVideoPlayerForRegular()
	{
		if (!((Object)(object)videoPlayer == (Object)null))
		{
			videoPlayer.aspectRatio = (VideoAspectRatio)1;
			Debug.Log((object)("Configured video player for regular videos with FitVertically aspect ratio for " + ((Object)((Component)this).gameObject).name));
		}
	}

	public bool IsPlayingShorts()
	{
		return isPlayingShorts;
	}

	public void PlayVideoAtTime(string url, double time, bool loop)
	{
		if (!isInitialized)
		{
			Debug.LogWarning((object)("YouTubeTVController.PlayVideoAtTime: Not initialized for " + ((Object)((Component)this).gameObject).name + ". Cannot play."));
			return;
		}
		Debug.Log((object)$"YouTubeTVController.PlayVideoAtTime: Called for {((Object)((Component)this).gameObject).name}. URL: '{url}', Time: {time}, Loop: {loop}");
		videoPlayer.isLooping = loop;
		if (videoPlayer.url != url || !videoPlayer.isPrepared || string.IsNullOrEmpty(videoPlayer.url))
		{
			Debug.Log((object)$"YouTubeTVController.PlayVideoAtTime: URL ('{url}') is different or player not prepared/url empty. Current URL: '{videoPlayer.url}'. Calling PlayYouTubeVideo. Pending seek to: {time}");
			pendingStartTime = time;
			PlayYouTubeVideo(url);
			return;
		}
		Debug.Log((object)$"YouTubeTVController.PlayVideoAtTime: Same URL ('{url}') and prepared. Seeking to {time} and playing.");
		videoPlayer.time = time;
		if (videoPlayer.isPrepared)
		{
			videoPlayer.Play();
			isPlaying = true;
			SetScreenToVideo();
		}
		else
		{
			Debug.LogWarning((object)("YouTubeTVController.PlayVideoAtTime: Player was thought to be prepared for '" + url + "', but isn't. Playback may not start."));
		}
	}

	public void PauseVideoAtTime(double time)
	{
		if (!isInitialized || (Object)(object)videoPlayer == (Object)null)
		{
			Debug.LogWarning((object)("YouTubeTVController.PauseVideoAtTime: Not initialized or no videoPlayer for " + ((Object)((Component)this).gameObject).name + "."));
			return;
		}
		Debug.Log((object)$"YouTubeTVController.PauseVideoAtTime: Called for {((Object)((Component)this).gameObject).name}. ServerTime: {time}, CurrentPlayerTime: {videoPlayer.time}");
		videoPlayer.Pause();
		isPlaying = false;
	}

	public void SeekVideoToTime(double time, bool resumePlaying)
	{
		if (!isInitialized || (Object)(object)videoPlayer == (Object)null)
		{
			Debug.LogWarning((object)("YouTubeTVController.SeekVideoToTime: Not initialized or no videoPlayer for " + ((Object)((Component)this).gameObject).name + "."));
			return;
		}
		Debug.Log((object)$"YouTubeTVController.SeekVideoToTime: Called for {((Object)((Component)this).gameObject).name}. TargetTime: {time}, ResumePlaying: {resumePlaying}");
		if (!videoPlayer.isPrepared)
		{
			Debug.LogWarning((object)("YouTubeTVController.SeekVideoToTime: VideoPlayer not prepared for '" + videoPlayer.url + "'. Seeking may not work as expected. Current URL: " + videoPlayer.url));
		}
		videoPlayer.time = time;
		if (resumePlaying && videoPlayer.isPrepared)
		{
			Debug.Log((object)("YouTubeTVController.SeekVideoToTime: Resuming playback for " + ((Object)((Component)this).gameObject).name + "."));
			videoPlayer.Play();
			isPlaying = true;
			SetScreenToVideo();
			return;
		}
		if (resumePlaying && !videoPlayer.isPrepared)
		{
			Debug.LogWarning((object)("YouTubeTVController.SeekVideoToTime: Told to resume, but player not prepared for " + ((Object)((Component)this).gameObject).name + ". Playback will likely not start."));
			isPlaying = false;
			return;
		}
		Debug.Log((object)("YouTubeTVController.SeekVideoToTime: Not resuming playback for " + ((Object)((Component)this).gameObject).name + " (or player not prepared)."));
		if (videoPlayer.isPlaying && !resumePlaying)
		{
			videoPlayer.Pause();
		}
		isPlaying = false;
	}

	private void SetScreenToVideo()
	{
		if ((Object)(object)tvScreenRenderer != (Object)null && (Object)(object)videoRenderTexture != (Object)null && videoRenderTexture.IsCreated())
		{
			tvScreenRenderer.material.SetTexture(tvScreenMaterialPropertyName, (Texture)(object)videoRenderTexture);
		}
		else
		{
			Debug.LogWarning((object)("YouTubeTVController.SetScreenToVideo: Cannot set screen to video - renderer or render texture issue for " + ((Object)((Component)this).gameObject).name + "."));
		}
	}

	private void OnDestroy()
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		string name = ((Object)((Component)this).gameObject).name;
		RenderTexture obj = videoRenderTexture;
		Debug.Log((object)("YouTubeTVController.OnDestroy: Called for " + name + ". Current RT: " + ((obj != null) ? ((Object)obj).name : null)));
		if (ParentTileEntity != null)
		{
			Vector3i val = ((TileEntity)ParentTileEntity).ToWorldPos();
			YouTubeTVManager.Instance.UnregisterTV(val);
			Debug.Log((object)$"YouTubeTVController.OnDestroy: Unregistered TV from manager at block position {val}");
		}
		StopAllPlayback();
		CleanupShortsFiles();
		if ((Object)(object)videoPlayer != (Object)null)
		{
			videoPlayer.prepareCompleted -= new VideoPlayer.EventHandler(OnVideoPrepared);
			videoPlayer.loopPointReached -= new VideoPlayer.EventHandler(OnVideoEnd);
		}
		if ((Object)(object)videoRenderTexture != (Object)null)
		{
			Debug.Log((object)("YouTubeTVController.OnDestroy: Releasing and destroying RenderTexture '" + ((Object)videoRenderTexture).name + "' for " + ((Object)((Component)this).gameObject).name + "."));
			if (videoRenderTexture.IsCreated())
			{
				videoRenderTexture.Release();
			}
			Object.Destroy((Object)(object)videoRenderTexture);
			videoRenderTexture = null;
		}
		if ((Object)(object)blackTexture != (Object)null)
		{
			Object.Destroy((Object)(object)blackTexture);
			blackTexture = null;
		}
		if ((Object)(object)idlePlaceholderTexture != (Object)null)
		{
			Object.Destroy((Object)(object)idlePlaceholderTexture);
		}
		if ((Object)(object)loadingPlaceholderTexture != (Object)null)
		{
			Object.Destroy((Object)(object)loadingPlaceholderTexture);
		}
		string path = Path.Combine(Application.persistentDataPath, downloadFolder);
		if (!Directory.Exists(path))
		{
			return;
		}
		try
		{
			string[] files = Directory.GetFiles(path, "video_*.mp4");
			for (int i = 0; i < files.Length; i++)
			{
				File.Delete(files[i]);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("Failed to clean up video files: " + ex.Message));
		}
	}
}
