using System;using System.IO;using System.Reflection;using Microsoft.Win32;using PipDesktop;
class LifeTests {
 static int checks;static void Check(bool b,string text){if(!b)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static FieldInfo F(string name){return typeof(PetForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
 static object Call(PetForm p,string method,params object[] args){return typeof(PetForm).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,args);}
 [STAThread]static void Main(){
  LifeState l=new LifeState();Check(l.Level==1,"friendship starts at level one");for(int i=0;i<20;i++)l.Reward("moment");Check(l.Level==3,"friendship unlock levels");for(int i=0;i<50;i++)l.Note("entry");Check(l.diary.Count==40,"diary stays bounded");
  string code="PIP1."+RelayCrypto.NewSecret()+".0123456789abcdef";string sealedText=RelayCrypto.Seal(code,"Пип в гостях 🦊");Check(RelayCrypto.Open(code,sealedText)=="Пип в гостях 🦊","encrypted UTF8 roundtrip");bool rejected=false;try{RelayCrypto.Open(code,sealedText.Substring(0,sealedText.Length-4)+"AAAA");}catch{rejected=true;}Check(rejected,"tampered packet rejected");rejected=false;try{RelayCrypto.Secret("random-public-topic");}catch{rejected=true;}Check(rejected,"invalid friend code rejected");Check(RelayCrypto.Topic(code).Length==47,"opaque broker topic");
  string key=@"Software\PipDesktop.Tests\"+Guid.NewGuid().ToString("N");try{
   using(PetForm p=new PetForm(key,false)){Call(p,"Reward","hello");Call(p,"Reward","spam");LifeState state=(LifeState)F("life").GetValue(p);Check(state.points==1,"friendship reward cooldown");Call(p,"StartFocus",25);Call(p,"UpdateLife",40,DateTime.UtcNow);Check((DateTime)F("focusEnd").GetValue(p)>DateTime.UtcNow.AddMinutes(24),"focus keeps working interval");F("focusEnd").SetValue(p,DateTime.UtcNow.AddSeconds(-1));Call(p,"UpdateLife",40,DateTime.UtcNow);Check((bool)F("focusBreak").GetValue(p),"focus transitions to break");F("focusEnd").SetValue(p,DateTime.UtcNow.AddSeconds(-1));Call(p,"UpdateLife",40,DateTime.UtcNow);Check((DateTime)F("focusEnd").GetValue(p)==DateTime.MinValue,"break finishes without unwanted restart");Call(p,"Paw");Call(p,"UpdateLife",40,DateTime.UtcNow);Check((int)F("row").GetValue(p)==3,"paw uses waving frames");Call(p,"SaveSettings");}
   using(PetForm p=new PetForm(key,false)){LifeState state=(LifeState)F("life").GetValue(p);Check(state.points==1&&state.diary.Count>=3,"friendship and diary survive restart");}
  }finally{Registry.CurrentUser.DeleteSubKeyTree(key,false);}
  Console.WriteLine("ALL "+checks+" CHECKS PASSED");
 }
}
