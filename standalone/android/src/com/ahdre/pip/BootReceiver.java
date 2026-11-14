package com.ahdre.pip;
import android.content.*;
import android.provider.Settings;
public class BootReceiver extends BroadcastReceiver {
 @Override public void onReceive(Context c,Intent i){
  android.content.SharedPreferences p=c.getSharedPreferences("pip",0);
  if(p.getBoolean("enabled",false)&&p.getBoolean("autostart",true)&&Settings.canDrawOverlays(c)){
   try{c.startForegroundService(new Intent(c,PetService.class));}catch(RuntimeException e){android.util.Log.w("Pip","Autostart blocked by device policy",e);}
  }
 }
}
