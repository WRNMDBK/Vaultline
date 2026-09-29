import app.vaultline.VaultCore;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

public final class CoreTest {
 static int count;interface Attempt{void run()throws Exception;}
 static void check(boolean value,String name)throws Exception{if(!value)throw new Exception("FAIL: "+name);count++;}
 static void reject(Attempt a,String name)throws Exception{boolean failed=false;try{a.run();}catch(Exception ex){failed=true;}check(failed,name);}
 public static void main(String[] args)throws Exception{
  File out=new File(args[0]);out.mkdirs();
  VaultCore.Vault v=new VaultCore.Vault();JSONObject fields=new JSONObject("{\"address\":\"demo@example.test\",\"password\":\"sample-secret\",\"nested\":{\"a/b\":42},\"cards\":[\"001\",true,null]}");
  VaultCore.Entry entry=new VaultCore.Entry("account/wechat/work-account","机密",fields,VaultCore.now());VaultCore.put(v,entry,null);
  check(v.folders.size()==2,"parent groups");VaultCore.validate(v);
  check("sample-secret".equals(VaultCore.resolve(entry,entry.path+"/password")),"path field search");
  check(((Number)VaultCore.resolve(entry,entry.path+"/nested/a~1b")).intValue()==42,"escaped nested key");
  check("001".equals(VaultCore.resolve(entry,entry.path+"/cards/0")),"array indexing");
  reject(()->VaultCore.resolve(entry,entry.path+"/cards/99"),"invalid index rejected");
  reject(()->VaultCore.path("a/../b"),"invalid path rejected");
  reject(()->VaultCore.put(v,entry.copy(),null),"duplicate label rejected");
  reject(()->VaultCore.addFolder(v,entry.path+"/child"),"record-folder conflict rejected");
  check(VaultCore.copyLines(fields).contains("sample-secret")&&!VaultCore.copyLines(fields).contains("\"password\""),"plain line copy");
  check(VaultCore.copyLines("00pass\"word").equals("00pass\"word"),"scalar copy unchanged");
  VaultCore.addFolder(v,"empty/group");String json=VaultCore.export(v).toString(2);VaultCore.Vault imported=VaultCore.parseExport(json);
  check(imported.entries.size()==1&&imported.folders.contains("empty/group"),"export import roundtrip");
  check(VaultCore.canonical(imported.entries.get(0).fields).equals(VaultCore.canonical(fields)),"preserve nested JSON types");
  VaultCore.Merge first=VaultCore.merge(new VaultCore.Vault(),imported);check(first.added==1,"fresh device import");
  VaultCore.Merge again=VaultCore.merge(first.vault,imported);check(again.added==0&&again.skipped==1,"duplicate import skipped");
  VaultCore.Vault changed=imported.copy();changed.entries.get(0).fields.put("password","changed");
  VaultCore.Merge conflict=VaultCore.merge(imported,changed);check(conflict.added==1&&conflict.renamed==1&&conflict.vault.entries.size()==2,"conflicts preserve both");
  check(VaultCore.merge(conflict.vault,changed).added==0,"repeat conflict import skipped");
  check("sample-secret".equals(imported.entries.get(0).fields.getString("password")),"original unchanged");
  VaultCore.Vault occupied=new VaultCore.Vault();VaultCore.put(occupied,new VaultCore.Entry("account","普通",new JSONObject(),null),null);
  VaultCore.Merge collision=VaultCore.merge(occupied,imported);check(collision.vault.entries.size()==2&&collision.vault.folders.contains("account-import-2/wechat"),"folder conflicts retain hierarchy");
  VaultCore.Vault moved=imported.copy();VaultCore.moveFolder(moved,"account/wechat","工作/微信");check(VaultCore.entry(moved,"工作/微信/work-account")!=null,"move whole group");
  reject(()->VaultCore.parseExport("{}"),"foreign file rejected");
  reject(()->VaultCore.parseExport(json.replace("Vaultline.Export","Other")),"foreign format rejected");
  JSONObject bad=VaultCore.export(v);bad.put("version",99);reject(()->VaultCore.parseExport(bad.toString()),"unsupported version rejected");
  byte[] salt=VaultCore.random(16),key=VaultCore.derive("sample-master-中文-123".toCharArray(),salt),plain=VaultCore.serialize(v),blob=VaultCore.seal(plain,key,salt);
  check(Arrays.equals(plain,VaultCore.open(blob,key)),"authenticated encryption roundtrip");
  check(!Arrays.equals(blob,VaultCore.seal(plain,key,salt)),"fresh IV per write");
  byte[] tampered=blob.clone();tampered[44]^=1;reject(()->VaultCore.open(tampered,key),"tamper rejected");
  reject(()->VaultCore.open(blob,new byte[64]),"wrong key rejected");
  reject(()->VaultCore.salt(new byte[12]),"truncation rejected");
  File file=new File(out,"test-vault.jx");VaultCore.atomic(file,blob);VaultCore.atomic(file,VaultCore.seal(plain,key,salt));check(new File(file+".bak").exists(),"atomic backup created");
  check(VaultCore.deserialize(VaultCore.open(Files.readAllBytes(file.toPath()),key)).entries.size()==1,"persisted encrypted data reload");
  VaultCore.Vault desktop=VaultCore.parseExport(new String(Files.readAllBytes(new File(out,"desktop-export.json").toPath()),StandardCharsets.UTF_8));check(desktop.entries.size()==2,"Windows export imports on Android");
  byte[] desktopBlob=Files.readAllBytes(new File(out,"desktop-vault.jx").toPath());byte[] desktopKey=VaultCore.derive("sample-master-中文-123".toCharArray(),VaultCore.salt(desktopBlob));
  VaultCore.Vault decrypted=VaultCore.deserialize(VaultCore.open(desktopBlob,desktopKey));check(decrypted.entries.size()==2,"Windows encryption and Unicode password decrypt on Android");
  Files.write(new File(out,"android-export.json").toPath(),VaultCore.export(decrypted).toString(2).getBytes(StandardCharsets.UTF_8));
  Files.write(new File(out,"android-vault.jx").toPath(),VaultCore.seal(VaultCore.serialize(decrypted),key,salt));
  Arrays.fill(key,(byte)0);Arrays.fill(desktopKey,(byte)0);Arrays.fill(plain,(byte)0);
  Files.write(new File(out,"android-test-results.txt").toPath(),("PASS "+count+" Android core assertions\n").getBytes(StandardCharsets.UTF_8));
  System.out.println("PASS "+count+" Android core assertions");
 }
}
