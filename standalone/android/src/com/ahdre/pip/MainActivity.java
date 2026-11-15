package com.ahdre.pip;

import android.app.*;
import android.os.*;
import android.content.*;
import android.graphics.Color;
import android.net.Uri;
import android.provider.Settings;
import android.view.*;
import android.widget.*;
import java.net.*;
import java.io.*;
import javax.net.ssl.HttpsURLConnection;
import org.json.JSONObject;

public class MainActivity extends Activity {
 public static final String VERSION="1.1.0";
 private TextView status;private SharedPreferences prefs;private boolean requestedStart,checking;
 int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
 @Override public void onCreate(Bundle state){
  super.onCreate(state);prefs=getSharedPreferences("pip",0);
  LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setPadding(dp(20),dp(38),dp(20),dp(32));page.setBackgroundColor(Color.rgb(242,249,248));
  ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.addView(page);setContentView(scroll);
  ImageView icon=new ImageView(this);icon.setImageResource(R.drawable.pip_icon);icon.setAdjustViewBounds(true);LinearLayout.LayoutParams ip=new LinearLayout.LayoutParams(dp(100),dp(108));ip.gravity=Gravity.CENTER_HORIZONTAL;page.addView(icon,ip);
  TextView title=label(page,"Пип рядом 🦊 · "+VERSION,25);title.setGravity(Gravity.CENTER);title.setTextColor(Color.rgb(15,98,101));
  label(page,"Пип живёт без ChatGPT и интернета. Проверка обновлений — только по кнопке.",15);
  status=label(page,"",14);
  buttonRow(page,new String[]{"Показать Пипа","Спрятать Пипа"},new String[]{"SHOW","STOP"});
  buttonRow(page,new String[]{"Погладить","Поиграть"},new String[]{"PET","PLAY"});
  buttonRow(page,new String[]{"Уложить спать","Не мешать 30 мин"},new String[]{"SLEEP","QUIET"});
  label(page,"Настройки поведения",20);
  select(page,"Размер",new String[]{"Маленький","Обычный","Большой"},"size",new int[]{80,104,136},104);
  select(page,"Активность",new String[]{"Спокойный","Обычный","Игривый"},"activity",new int[]{0,1,2},1);
  select(page,"Скорость",new String[]{"Медленно","Обычно","Быстро"},"speed",new int[]{14,28,52},28);
  Switch boot=new Switch(this);boot.setText("Запускать с телефоном");boot.setTextSize(16);boot.setChecked(prefs.getBoolean("autostart",true));boot.setPadding(0,dp(12),0,dp(12));boot.setOnCheckedChangeListener(new CompoundButton.OnCheckedChangeListener(){public void onCheckedChanged(CompoundButton b,boolean checked){prefs.edit().putBoolean("autostart",checked).apply();}});page.addView(boot);
  label(page,"Пип запоминает место и размер. Без внимания засыпает через полторы минуты. Погладь его короткими движениями туда-сюда или кнопкой выше.\n\nОдин тап — приветствие, два — прыжок. Долгое нажатие открывает управление. «Показать Пипа» отменяет режим тишины.",15);
  addButton(page,"Проверить обновления",new View.OnClickListener(){public void onClick(View v){checkUpdates();}});
  addButton(page,"Разрешение поверх приложений",new View.OnClickListener(){public void onClick(View v){startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:"+getPackageName())));}});
  label(page,"На некоторых телефонах автозапуск нужно дополнительно разрешить в системных настройках. Во время выключенного экрана Пип отдыхает.",13);
  refresh();
 }
 TextView label(LinearLayout page,String text,int size){TextView t=new TextView(this);t.setText(text);t.setTextSize(size);t.setTextColor(Color.DKGRAY);t.setPadding(0,dp(9),0,dp(9));page.addView(t);return t;}
 void buttonRow(LinearLayout page,String[] titles,String[] actions){LinearLayout row=new LinearLayout(this);row.setOrientation(0);for(int i=0;i<titles.length;i++){final String action=actions[i];Button b=new Button(this);b.setText(titles[i]);b.setAllCaps(false);b.setTextSize(14);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(0,dp(60),1);row.addView(b,p);b.setOnClickListener(new View.OnClickListener(){public void onClick(View v){act(action);}});}page.addView(row);}
 void addButton(LinearLayout page,String text,View.OnClickListener action){Button b=new Button(this);b.setText(text);b.setAllCaps(false);b.setOnClickListener(action);page.addView(b,new LinearLayout.LayoutParams(-1,dp(58)));}
 void select(LinearLayout page,String title,String[] labels,final String key,final int[] values,int fallback){label(page,title,15);Spinner spinner=new Spinner(this);ArrayAdapter<String> a=new ArrayAdapter<String>(this,android.R.layout.simple_spinner_item,labels);a.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);spinner.setAdapter(a);int selected=0,current=prefs.getInt(key,fallback);for(int i=0;i<values.length;i++)if(values[i]==current)selected=i;spinner.setSelection(selected);page.addView(spinner,new LinearLayout.LayoutParams(-1,dp(48)));spinner.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener(){public void onItemSelected(AdapterView<?> p,View v,int index,long id){int old=prefs.getInt(key,-1);if(old==values[index])return;prefs.edit().putInt(key,values[index]).apply();if(prefs.getBoolean("enabled",false)&&Settings.canDrawOverlays(MainActivity.this))startForegroundService(new Intent(MainActivity.this,PetService.class).setAction("CONFIG"));}public void onNothingSelected(AdapterView<?> p){}});}
 void act(String action){
  if("STOP".equals(action)){prefs.edit().putBoolean("enabled",false).apply();stopService(new Intent(this,PetService.class));refresh();return;}
  if(!Settings.canDrawOverlays(this)){requestedStart=true;startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:"+getPackageName())));return;}
  if(Build.VERSION.SDK_INT>=33&&checkSelfPermission("android.permission.POST_NOTIFICATIONS")!=0)requestPermissions(new String[]{"android.permission.POST_NOTIFICATIONS"},42);
  SharedPreferences.Editor edit=prefs.edit().putBoolean("enabled",true);if(!"QUIET".equals(action))edit.putLong("quietUntil",0);edit.apply();startForegroundService(new Intent(this,PetService.class).setAction(action));refresh();
 }
 void refresh(){if(status==null)return;String s=!Settings.canDrawOverlays(this)?"Нужно разрешение поверх приложений":!prefs.getBoolean("enabled",false)?"Пип спрятан":prefs.getLong("quietUntil",0)>System.currentTimeMillis()?"Пип отдыхает в режиме «не мешать»":"Пип готов к общению";status.setText(s);}
 public static int compareVersions(String a,String b){String[] x=a.split("\\."),y=b.split("\\.");for(int i=0;i<Math.max(x.length,y.length);i++){int l=i<x.length?Integer.parseInt(x[i]):0,r=i<y.length?Integer.parseInt(y[i]):0;if(l!=r)return l<r?-1:1;}return 0;}
 void checkUpdates(){
  if(checking)return;checking=true;Toast.makeText(this,"Проверяю версию на GitHub…",Toast.LENGTH_SHORT).show();
  new Thread(new Runnable(){public void run(){
   String result;boolean newer=false;HttpsURLConnection connection=null;
   try{connection=(HttpsURLConnection)new URL("https://raw.githubusercontent.com/Ahdrej545/pip-pet/main/standalone/version.json").openConnection();connection.setConnectTimeout(8000);connection.setReadTimeout(8000);connection.setRequestProperty("User-Agent","Pip/"+VERSION);ByteArrayOutputStream bytes=new ByteArrayOutputStream();try(InputStream in=connection.getInputStream()){byte[] buffer=new byte[1024];int count;while((count=in.read(buffer))!=-1){if(bytes.size()+count>65536)throw new IOException("Response too large");bytes.write(buffer,0,count);}}String remote=new JSONObject(bytes.toString("UTF-8")).getString("version");newer=compareVersions(remote,VERSION)>0;result=newer?"Доступен Пип "+remote+". Открыть страницу скачивания?":"У тебя актуальная версия Пипа ("+VERSION+").";}
   catch(Exception e){result="Не удалось проверить обновления. Пип продолжит работать без интернета.";}finally{if(connection!=null)connection.disconnect();}
   final String message=result;final boolean offer=newer;
   runOnUiThread(new Runnable(){public void run(){checking=false;if(isFinishing()||isDestroyed())return;AlertDialog.Builder dialog=new AlertDialog.Builder(MainActivity.this).setTitle("Обновления Пипа").setMessage(message);if(offer)dialog.setPositiveButton("Открыть GitHub",new DialogInterface.OnClickListener(){public void onClick(DialogInterface d,int which){startActivity(new Intent(Intent.ACTION_VIEW,Uri.parse("https://github.com/Ahdrej545/pip-pet/tree/main/standalone")));}});else dialog.setPositiveButton("Понятно",null);dialog.show();}});
  }}).start();
 }
 @Override public void onResume(){super.onResume();refresh();if(requestedStart&&Settings.canDrawOverlays(this)){requestedStart=false;act("SHOW");}}
}
