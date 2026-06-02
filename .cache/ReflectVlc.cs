using System;
using System.Reflection;
using LibVLCSharp.Shared;
class P {
  static void Main(){
    foreach(var nt in typeof(MediaPlayer).GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)){
      if(nt.Name.Contains("Video")){
        var inv=nt.GetMethod("Invoke");
        if(inv!=null) Console.WriteLine(nt.FullName+" => "+inv.ReturnType+" "+string.Join(", ", Array.ConvertAll(inv.GetParameters(), p => p.ParameterType+" "+p.Name)));
      }
    }
  }
}
