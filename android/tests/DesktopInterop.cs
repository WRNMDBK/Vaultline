using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Vaultline;
class DesktopInterop {
 static int Main(string[] args){try{string folder=args[1];Directory.CreateDirectory(folder);if(args[0]=="generate"){
  var v=new Vault();Engine.AddFolder(v,"empty/group");foreach(string name in new[]{"work-account","social-account"})Engine.Put(v,new Entry{Path="account/wechat/"+name,Level="机密",Updated="2026-09-30",Fields=Engine.Json.Deserialize<Dictionary<string,object>>("{\"address\":\"demo@example.test\",\"password\":\"sample-secret\",\"nested\":{\"中文\":42},\"list\":[true,null,\"001\"]}")},null);
  File.WriteAllText(Path.Combine(folder,"desktop-export.json"),Engine.ExportAll(v),new UTF8Encoding(false));byte[] salt=Crypto.Random(16),key=Crypto.Derive("sample-master-中文-123",salt);File.WriteAllBytes(Path.Combine(folder,"desktop-vault.jx"),Crypto.Seal(Encoding.UTF8.GetBytes(Engine.Json.Serialize(v)),key,salt));Array.Clear(key,0,key.Length);Console.WriteLine("Generated synthetic Windows interoperability fixtures");
 }else{
  var v=Engine.ParseImport(File.ReadAllText(Path.Combine(folder,"android-export.json")));if(v.Entries.Count!=2||!v.Folders.Contains("empty/group"))throw new Exception("Android JSON import failed");
  byte[] blob=File.ReadAllBytes(Path.Combine(folder,"android-vault.jx")),key=Crypto.Derive("sample-master-中文-123",Crypto.Salt(blob));var decoded=Engine.Json.Deserialize<Vault>(Encoding.UTF8.GetString(Crypto.Open(blob,key)));Engine.Validate(decoded);if(decoded.Entries.Count!=2)throw new Exception("Android encrypted vault decode failed");Array.Clear(key,0,key.Length);
  File.WriteAllText(Path.Combine(folder,"desktop-interop-results.txt"),"PASS: Android JSON export and encrypted vault decrypt/import on Windows, including Unicode master password.");Console.WriteLine("PASS Android to Windows interoperability");
 }return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
}
