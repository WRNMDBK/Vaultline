using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Vaultline {
 public class Entry { public string Path {get;set;} public string Level {get;set;} public Dictionary<string,object> Fields {get;set;} public string Updated {get;set;} }
 public class Vault { public int Version {get;set;} public List<string> Folders {get;set;} public List<Entry> Entries {get;set;} public Vault(){Version=1;Folders=new List<string>();Entries=new List<Entry>();} }
 public class ImportResult {public Vault Value;public int Added,Skipped,Renamed;}
 public static class Engine {
  public static JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=16000000,RecursionLimit=64};
  public static string Normalize(string s){return (s??"").Trim().Trim('/');}
  public static bool Equal(string a,string b){return string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
  public static bool Under(string path,string folder){return folder==""||Equal(path,folder)||path.StartsWith(folder+"/",StringComparison.OrdinalIgnoreCase);}
  public static string Parent(string s){int i=s.LastIndexOf('/');return i<0?"":s.Substring(0,i);}
  public static string Name(string s){return s.Substring(s.LastIndexOf('/')+1);}
  public static void ValidatePath(string s){if(string.IsNullOrWhiteSpace(s)||s.Length>500||s!=Normalize(s)||s.Split('/').Any(p=>string.IsNullOrWhiteSpace(p)||p!=p.Trim()||p=="."||p==".."||p.Any(c=>char.IsControl(c)||c=='\\')))throw new Exception("路径不能为空；每级使用 / 分隔，不支持 .、..、反斜线或首尾空格。");}
  public static void AddFolder(Vault v,string p){ValidatePath(p);string t="";foreach(string bit in p.Split('/')){t=t==""?bit:t+"/"+bit;if(v.Entries.Any(e=>Equal(e.Path,t)))throw new Exception("该路径已被账号标签占用。");if(!v.Folders.Any(x=>Equal(x,t)))v.Folders.Add(t);}}
  public static void Put(Vault v,Entry e,Entry old){ValidatePath(e.Path);if(v.Entries.Any(x=>x!=old&&Equal(x.Path,e.Path))||v.Folders.Any(x=>Equal(x,e.Path)))throw new Exception("该路径已存在，请使用不同标签。");string p=Parent(e.Path);if(p!="")AddFolder(v,p);if(old!=null)v.Entries.Remove(old);v.Entries.Add(e);}
  public static void Validate(Vault v){if(v==null||v.Version!=1||v.Folders==null||v.Entries==null)throw new Exception("不支持的数据格式。");var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string f in v.Folders){ValidatePath(f);if(!paths.Add(f))throw new Exception("重复目录。");}foreach(Entry e in v.Entries){ValidatePath(e.Path);if(e.Fields==null||!paths.Add(e.Path))throw new Exception("重复账号或 JSON 无效。");}foreach(string p in paths){string parent=Parent(p);if(parent!=""&&!v.Folders.Any(f=>Equal(f,parent)))throw new Exception("缺少父目录。");}}
  public static object Resolve(Vault v,string query,out Entry entry){query=Normalize(query);entry=v.Entries.Where(e=>Equal(query,e.Path)||query.StartsWith(e.Path+"/",StringComparison.OrdinalIgnoreCase)).OrderByDescending(e=>e.Path.Length).FirstOrDefault();if(entry==null)return null;object value=entry.Fields;if(query.Length==entry.Path.Length)return value;foreach(string raw in query.Substring(entry.Path.Length+1).Split('/')){string key=raw.Replace("~1","/").Replace("~0","~");var dict=value as Dictionary<string,object>;if(dict!=null){if(!dict.TryGetValue(key,out value))throw new Exception("字段不存在："+key);}else{var a=value as IList;int index;if(a==null||!int.TryParse(key,out index)||index<0||index>=a.Count)throw new Exception("字段或数组下标不存在："+key);value=a[index];}}return value;}
  public static string Pretty(object o){return PrettyValue(o,0);}
  static string PrettyValue(object o,int depth){var d=o as IDictionary<string,object>;if(d!=null){if(d.Count==0)return "{}";return "{\r\n"+string.Join(",\r\n",d.Select(kv=>new string(' ',(depth+1)*2)+Json.Serialize(kv.Key)+": "+PrettyValue(kv.Value,depth+1)))+"\r\n"+new string(' ',depth*2)+"}";}var a=o as IList;if(a!=null){if(a.Count==0)return "[]";return "[\r\n"+string.Join(",\r\n",a.Cast<object>().Select(x=>new string(' ',(depth+1)*2)+PrettyValue(x,depth+1)))+"\r\n"+new string(' ',depth*2)+"]";}return Json.Serialize(o);}
  public static string CopyLines(object value){var lines=new List<string>();Flatten(value,"",false,lines);return string.Join(Environment.NewLine,lines);}
  public static string DisplayLines(object value){var lines=new List<string>();Flatten(value,"",true,lines);return string.Join(Environment.NewLine+Environment.NewLine,lines);}
  static void Flatten(object value,string path,bool labels,List<string> lines){var d=value as IDictionary<string,object>;if(d!=null){foreach(var pair in d)Flatten(pair.Value,path==""?pair.Key:path+" / "+pair.Key,labels,lines);return;}var a=value as IList;if(a!=null){for(int i=0;i<a.Count;i++)Flatten(a[i],path+"["+i+"]",labels,lines);return;}string text=value==null?"":value is string?(string)value:Json.Serialize(value);lines.Add(labels&&path!=""?path+"："+text:text);}
  public static string ExportGroup(Vault v,string folder){folder=Normalize(folder);if(folder!=""&&!v.Folders.Any(f=>Equal(f,folder)))throw new Exception("分组不存在。");var result=new Dictionary<string,object>{{"format","Vaultline.GroupExport"},{"version",1},{"group",folder},{"exportedAt",DateTime.UtcNow.ToString("o")},{"folders",v.Folders.Where(f=>Under(f,folder)).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase).ToArray()},{"entries",v.Entries.Where(e=>Under(e.Path,folder)).OrderBy(e=>e.Path,StringComparer.OrdinalIgnoreCase).Select(e=>new Dictionary<string,object>{{"path",e.Path},{"label",Name(e.Path)},{"level",e.Level},{"updated",e.Updated},{"fields",e.Fields}}).ToArray()}};return Pretty(result);}
  public static string ExportAll(Vault v){var data=Json.Deserialize<Dictionary<string,object>>(ExportGroup(v,""));data["format"]="Vaultline.Export";data.Remove("group");return Pretty(data);}
  public static Vault ParseImport(string text){
   var data=Json.DeserializeObject(text) as Dictionary<string,object>;object format,version,folders,entries;
   if(data==null||!data.TryGetValue("format",out format)||!(format is string)||((string)format!="Vaultline.Export"&&(string)format!="Vaultline.GroupExport")||!data.TryGetValue("version",out version)||!(version is int)||(int)version!=1||!data.TryGetValue("folders",out folders)||!(folders is IList)||!data.TryGetValue("entries",out entries)||!(entries is IList))throw new Exception("请选择 Vaultline 导出的有效 JSON 文件。");
   var result=new Vault();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(object item in (IList)folders){string path=item as string;ValidatePath(path);if(!seen.Add(path))throw new Exception("导入文件中存在重复分组。");AddFolder(result,path);}
   foreach(object item in (IList)entries){var entry=item as Dictionary<string,object>;object path,fields,level,updated;
    if(entry==null||!entry.TryGetValue("path",out path)||!(path is string)||!entry.TryGetValue("fields",out fields)||!(fields is Dictionary<string,object>)||!entry.TryGetValue("level",out level)||!(level is string)||!new[]{"普通","敏感","机密"}.Contains((string)level))throw new Exception("导入文件包含无效的账号数据。");
    entry.TryGetValue("updated",out updated);if(updated!=null&&!(updated is string))throw new Exception("账号更新时间格式无效。");
    Put(result,new Entry{Path=(string)path,Fields=(Dictionary<string,object>)fields,Level=(string)level,Updated=(string)updated},null);
   }Validate(result);return result;
  }
  static string Canonical(object value){var d=value as IDictionary<string,object>;if(d!=null)return "{"+string.Join(",",d.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>Json.Serialize(x.Key)+":"+Canonical(x.Value)))+"}";var a=value as IList;if(a!=null)return "["+string.Join(",",a.Cast<object>().Select(Canonical))+"]";return Json.Serialize(value);}
  static bool SameEntry(Entry a,Entry b){return a.Level==b.Level&&Canonical(a.Fields)==Canonical(b.Fields);}
  static string AvailablePath(Vault v,string path){string candidate=path;int suffix=2;while(v.Folders.Any(f=>Equal(f,candidate))||v.Entries.Any(e=>Equal(e.Path,candidate)))candidate=path+"-import-"+(suffix++);ValidatePath(candidate);return candidate;}
  public static ImportResult MergeImport(Vault current,Vault incoming){
   Validate(current);Validate(incoming);var result=new ImportResult{Value=Json.Deserialize<Vault>(Json.Serialize(current))};var v=result.Value;var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"",""}};
   foreach(string f in incoming.Folders.OrderBy(f=>f.Count(c=>c=='/')).ThenBy(f=>f,StringComparer.OrdinalIgnoreCase)){string parent=map[Parent(f)];string target=(parent==""?"":parent+"/")+Name(f);if(v.Entries.Any(e=>Equal(e.Path,target)))target=AvailablePath(v,target);AddFolder(v,target);map[f]=target;}
   foreach(Entry source in incoming.Entries){string parent=map[Parent(source.Path)];string path=(parent==""?"":parent+"/")+Name(source.Path);Entry existing=v.Entries.FirstOrDefault(e=>Equal(e.Path,path));
    if(existing!=null&&SameEntry(existing,source)){result.Skipped++;continue;}
    if(existing!=null&&v.Entries.Any(e=>Equal(Parent(e.Path),parent)&&e.Path.StartsWith(path+"-import-",StringComparison.OrdinalIgnoreCase)&&SameEntry(e,source))){result.Skipped++;continue;}
    string target=AvailablePath(v,path);if(!Equal(target,source.Path))result.Renamed++;
    var entry=Json.Deserialize<Entry>(Json.Serialize(source));entry.Path=target;Put(v,entry,null);result.Added++;
   }Validate(v);return result;
  }
  public static void WriteExport(string target,string text,string vaultFile){string full=Path.GetFullPath(target),vault=Path.GetFullPath(vaultFile);if(!string.Equals(Path.GetExtension(full),".json",StringComparison.OrdinalIgnoreCase))throw new Exception("导出文件必须使用 .json 扩展名。");if(Equal(full,vault)||full.StartsWith(vault+".",StringComparison.OrdinalIgnoreCase)||Equal(full,Path.Combine(Path.GetDirectoryName(vault),"vault.lock")))throw new Exception("不能覆盖正在使用的保险库文件。");byte[] plain=new UTF8Encoding(false).GetBytes(text);string temp=Path.Combine(Path.GetDirectoryName(full),".vaultline-export-"+Guid.NewGuid().ToString("N")+".tmp");try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(plain,0,plain.Length);stream.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{Array.Clear(plain,0,plain.Length);if(File.Exists(temp))File.Delete(temp);}}
 }
 public static class Crypto {
  public const int Iterations=600000;
  static byte[] Magic=Encoding.ASCII.GetBytes("JINXIA01"); // Existing v1 vault compatibility.
  public static byte[] Random(int n){var b=new byte[n];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return b;}
  public static byte[] Derive(string password,byte[] salt){using(var k=new Rfc2898DeriveBytes(password,salt,Iterations,HashAlgorithmName.SHA256))return k.GetBytes(64);}
  public static byte[] Seal(byte[] plain,byte[] key,byte[] salt){byte[] cipher,iv;using(var a=Aes.Create()){a.Key=key.Take(32).ToArray();a.GenerateIV();iv=a.IV;using(var enc=a.CreateEncryptor())cipher=enc.TransformFinalBlock(plain,0,plain.Length);}byte[] head=Magic.Concat(salt).Concat(iv).Concat(cipher).ToArray();using(var h=new HMACSHA256(key.Skip(32).ToArray()))return head.Concat(h.ComputeHash(head)).ToArray();}
  public static byte[] Salt(byte[] data){if(data.Length<88||!data.Take(8).SequenceEqual(Magic))throw new Exception("不是有效的 Vaultline 加密文件。");return data.Skip(8).Take(16).ToArray();}
  public static byte[] Open(byte[] data,byte[] key){Salt(data);int n=data.Length-32;byte[] mac;using(var h=new HMACSHA256(key.Skip(32).ToArray()))mac=h.ComputeHash(data,0,n);int diff=0;for(int i=0;i<32;i++)diff|=mac[i]^data[n+i];if(diff!=0)throw new Exception("主密码不正确，或文件已损坏。");using(var a=Aes.Create()){a.Key=key.Take(32).ToArray();a.IV=data.Skip(24).Take(16).ToArray();using(var dec=a.CreateDecryptor())return dec.TransformFinalBlock(data,40,n-40);}}
  public static void Atomic(string path,byte[] data){string temp=path+".tmp";try{using(var fs=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){fs.Write(data,0,data.Length);fs.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 }
 public sealed class Store:IDisposable {
  public string FilePath;public Vault Value;byte[] key,salt;FileStream lease;
  public Store(string dir){Directory.CreateDirectory(dir);FilePath=Path.Combine(dir,"vault.jx");lease=new FileStream(Path.Combine(dir,"vault.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
  public void Unlock(string password){byte[] nextKey=null;try{if(File.Exists(FilePath)){byte[] blob=File.ReadAllBytes(FilePath);salt=Crypto.Salt(blob);nextKey=Crypto.Derive(password,salt);byte[] plain=Crypto.Open(blob,nextKey);try{Value=Engine.Json.Deserialize<Vault>(Encoding.UTF8.GetString(plain));Engine.Validate(Value);}finally{Array.Clear(plain,0,plain.Length);}}else{salt=Crypto.Random(16);nextKey=Crypto.Derive(password,salt);Value=new Vault();}key=nextKey;}catch{if(nextKey!=null)Array.Clear(nextKey,0,nextKey.Length);throw;}}
  public void Save(){Engine.Validate(Value);byte[] plain=Encoding.UTF8.GetBytes(Engine.Json.Serialize(Value));try{Crypto.Atomic(FilePath,Crypto.Seal(plain,key,salt));}finally{Array.Clear(plain,0,plain.Length);}}
  public void ChangePassword(string p){byte[] previous=key,oldSalt=salt;key=Crypto.Derive(p,salt=Crypto.Random(16));try{Save();Array.Clear(previous,0,previous.Length);}catch{Array.Clear(key,0,key.Length);key=previous;salt=oldSalt;throw;}}
  public void Lock(){if(key!=null)Array.Clear(key,0,key.Length);key=null;Value=null;}
  public void Dispose(){Lock();if(lease!=null)lease.Dispose();}
 }
}
