package com.ahdre.pip;

import android.app.*;
import android.os.*;
import android.content.*;
import android.graphics.Color;
import android.net.Uri;
import android.provider.Settings;
import android.view.*;
import android.widget.*;

public class MainActivity extends Activity {
  private TextView status;
  private boolean requestedStart;
  int dp(float n){return (int)(n*getResources().getDisplayMetrics().density+.5f);}
  @Override public void onCreate(Bundle state){
    super.onCreate(state);
    LinearLayout page=new LinearLayout(this);page.setOrientation(1);page.setPadding(dp(24),dp(36),dp(24),dp(24));page.setBackgroundColor(Color.rgb(242,249,248));
    ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.addView(page);setContentView(scroll);
    ImageView icon=new ImageView(this);icon.setImageResource(com.ahdre.pip.R.drawable.pip_icon);icon.setAdjustViewBounds(true);LinearLayout.LayoutParams ip=new LinearLayout.LayoutParams(dp(150),dp(160));ip.gravity=Gravity.CENTER_HORIZONTAL;page.addView(icon,ip);
    TextView title=new TextView(this);title.setText("Пип рядом 🦊");title.setTextSize(30);title.setTextColor(Color.rgb(15,98,101));title.setGravity(Gravity.CENTER);page.addView(title);
    TextView intro=new TextView(this);intro.setText("Твой лисёнок на экране. Работает без ChatGPT и интернета.");intro.setTextSize(17);intro.setPadding(0,dp(12),0,dp(16));page.addView(intro);
    status=new TextView(this);status.setTextSize(15);page.addView(status);
    addButton(page,"Показать Пипа",new View.OnClickListener(){public void onClick(View v){showPet();}});
    addButton(page,"Спрятать Пипа",new View.OnClickListener(){public void onClick(View v){getSharedPreferences("pip",0).edit().putBoolean("enabled",false).apply();stopService(new Intent(MainActivity.this,PetService.class));refresh();}});
    addButton(page,"Размер: меньше / больше",new View.OnClickListener(){public void onClick(View v){if(Settings.canDrawOverlays(MainActivity.this)){Intent i=new Intent(MainActivity.this,PetService.class).setAction("SIZE");startForegroundService(i);}}});
    Switch boot=new Switch(this);boot.setText("Запускать после включения телефона");boot.setTextSize(16);boot.setChecked(getSharedPreferences("pip",0).getBoolean("autostart",true));boot.setPadding(0,dp(18),0,dp(18));boot.setOnCheckedChangeListener(new CompoundButton.OnCheckedChangeListener(){public void onCheckedChanged(CompoundButton b,boolean checked){getSharedPreferences("pip",0).edit().putBoolean("autostart",checked).apply();}});page.addView(boot);
    TextView help=new TextView(this);help.setText("Перетащи Пипа пальцем. Нажми — помашет лапой, дважды — прыгнет. Долгое нажатие откроет это окно.\n\nОстановить его можно здесь или из уведомления. При выключенном экране он отдыхает.\n\nНа Xiaomi/Redmi автозапуск может потребовать разрешения в настройках телефона.");help.setTextSize(15);help.setTextColor(Color.DKGRAY);page.addView(help);
    addButton(page,"Разрешение поверх приложений",new View.OnClickListener(){public void onClick(View v){startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:"+getPackageName())));}});
    refresh();
  }
  void addButton(LinearLayout page,String label,View.OnClickListener action){Button b=new Button(this);b.setText(label);b.setAllCaps(false);b.setTextSize(16);b.setOnClickListener(action);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,dp(58));p.topMargin=dp(8);page.addView(b,p);}
  void refresh(){status.setText(Settings.canDrawOverlays(this)?"Разрешение поверх приложений включено":"Для Пипа нужно разрешение поверх приложений");}
  void showPet(){
    if(!Settings.canDrawOverlays(this)){requestedStart=true;startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:"+getPackageName())));return;}
    if(Build.VERSION.SDK_INT>=33&&checkSelfPermission("android.permission.POST_NOTIFICATIONS")!=0)requestPermissions(new String[]{"android.permission.POST_NOTIFICATIONS"},42);
    getSharedPreferences("pip",0).edit().putBoolean("enabled",true).apply();startForegroundService(new Intent(this,PetService.class));refresh();
  }
  @Override public void onResume(){super.onResume();if(status!=null)refresh();if(requestedStart&&Settings.canDrawOverlays(this)){requestedStart=false;showPet();}}
}
