package com.ahdre.pip;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.content.res.Configuration;
import android.graphics.*;
import android.os.*;
import android.provider.Settings;
import android.view.*;
import java.io.*;
import java.util.Random;

public class PetService extends Service {
 V12Features extra;LifeFeatures life;
 WindowManager wm;WindowManager.LayoutParams lp;PetView pet;Bitmap atlas;Handler handler=new Handler(Looper.getMainLooper());Random random=new Random();
 boolean screenOn=true,dragging=false;int row=0,frame=0,age=0,stateAge=0,limit=4000,direction=1,width,height,ground;
 long last;int sizeDp=104;float posX; boolean overlayAdded=false;int drawnRow=-1,drawnFrame=-1;
 SharedPreferences prefs;long touched=SystemClock.uptimeMillis(),happyUntil=0,playUntil=0,quietUntil=0,savedAt=0;int activity=1,speed=28;boolean sleeping=false;
 final int[][] durations={{280,110,110,140,140,320},{120,120,120,120,120,120,120,220},{120,120,120,120,120,120,120,220},{140,140,140,280},{140,140,140,140,280},{140,140,140,140,140,140,140,240},{150,150,150,150,150,260},{120,120,120,120,120,220},{150,150,150,150,150,260}};
 final BroadcastReceiver screenReceiver=new BroadcastReceiver(){public void onReceive(Context c,Intent i){String a=i.getAction();if(Intent.ACTION_SCREEN_OFF.equals(a)){screenOn=false;handler.removeCallbacks(loop);if(pet!=null)pet.setVisibility(View.INVISIBLE);if(extra!=null)extra.visible(false);if(life!=null)life.visible(false);}else if(Intent.ACTION_USER_PRESENT.equals(a)||Intent.ACTION_SCREEN_ON.equals(a)){screenOn=true;KeyguardManager k=(KeyguardManager)getSystemService(KEYGUARD_SERVICE);if(k==null||!k.isKeyguardLocked()){if(pet!=null)pet.setVisibility(View.VISIBLE);last=SystemClock.uptimeMillis();handler.removeCallbacks(loop);handler.post(loop);}}}};
 @Override public void onCreate(){super.onCreate();
  NotificationManager n=(NotificationManager)getSystemService(NOTIFICATION_SERVICE);NotificationChannel channel=new NotificationChannel("pip_pet","Пип на экране",NotificationManager.IMPORTANCE_LOW);channel.setSound(null,null);n.createNotificationChannel(channel);
  Intent settings=new Intent(this,MainActivity.class);PendingIntent open=PendingIntent.getActivity(this,1,settings,PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
  PendingIntent stop=PendingIntent.getService(this,2,new Intent(this,PetService.class).setAction("STOP"),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
  PendingIntent quiet=PendingIntent.getService(this,3,new Intent(this,PetService.class).setAction("QUIET"),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
  Notification notification=new Notification.Builder(this,"pip_pet").setSmallIcon(android.R.drawable.ic_menu_view).setContentTitle("Пип рядом 🦊 · 1.4").setContentText("Мячик, домик и компания — в настройках").setOngoing(true).setContentIntent(open).addAction(new Notification.Action.Builder(null,"Тишина 30 мин",quiet).build()).addAction(new Notification.Action.Builder(null,"Спрятать",stop).build()).addAction(new Notification.Action.Builder(null,"Настройки",open).build()).build();
  if(Build.VERSION.SDK_INT>=34)startForeground(17,notification,ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);else startForeground(17,notification);
  if(!Settings.canDrawOverlays(this)){stopSelf();return;}
  try(InputStream in=getAssets().open("spritesheet.png")){atlas=BitmapFactory.decodeStream(in);}catch(IOException e){stopSelf();return;}
  prefs=getSharedPreferences("pip",0);reloadSettings();
  wm=(WindowManager)getSystemService(WINDOW_SERVICE);sizeDp=prefs.getInt("size",104);measure();
  lp=new WindowManager.LayoutParams(dp(sizeDp),dp(sizeDp)*208/192,WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE|WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,PixelFormat.TRANSLUCENT);
  lp.gravity=Gravity.TOP|Gravity.LEFT;lp.x=Math.round(prefs.getFloat("posX",.90f)*Math.max(0,width-lp.width));lp.y=Math.round(prefs.getFloat("posY",.90f)*Math.max(0,height-lp.height));clamp();ground=lp.y;
  pet=new PetView();
  try{wm.addView(pet,lp);overlayAdded=true;}catch(RuntimeException e){android.util.Log.e("Pip","Cannot create overlay",e);stopSelf();return;}
  extra=new V12Features(this);life=new LifeFeatures(this);
  IntentFilter f=new IntentFilter();f.addAction(Intent.ACTION_SCREEN_OFF);f.addAction(Intent.ACTION_SCREEN_ON);f.addAction(Intent.ACTION_USER_PRESENT);if(Build.VERSION.SDK_INT>=33)registerReceiver(screenReceiver,f,Context.RECEIVER_NOT_EXPORTED);else registerReceiver(screenReceiver,f);
  PowerManager power=(PowerManager)getSystemService(POWER_SERVICE);KeyguardManager lock=(KeyguardManager)getSystemService(KEYGUARD_SERVICE);screenOn=power.isInteractive()&&!lock.isKeyguardLocked();if(!screenOn){pet.setVisibility(View.INVISIBLE);if(extra!=null)extra.visible(false);if(life!=null)life.visible(false);}
  last=SystemClock.uptimeMillis();if(screenOn)handler.post(loop);
 }
 int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
 void measure(){Rect b=wm==null?null:wm.getCurrentWindowMetrics().getBounds();if(b!=null){width=b.width();height=b.height();}else{width=getResources().getDisplayMetrics().widthPixels;height=getResources().getDisplayMetrics().heightPixels;}}
 @Override public int onStartCommand(Intent i,int flags,int startId){
  if(i!=null&&"STOP".equals(i.getAction())){getSharedPreferences("pip",0).edit().putBoolean("enabled",false).apply();stopSelf();return START_NOT_STICKY;}
  if(prefs!=null)reloadSettings();
  if(i!=null&&lp!=null){String action=i.getAction();if("QUIET".equals(action)){quietUntil=System.currentTimeMillis()+30*60*1000;prefs.edit().putLong("quietUntil",quietUntil).apply();pet.setVisibility(View.INVISIBLE);}else if("SHOW".equals(action)){quietUntil=0;prefs.edit().putLong("quietUntil",0).apply();touch();if(screenOn)pet.setVisibility(View.VISIBLE);}else if("PET".equals(action))petting();else if("PLAY".equals(action)){touch();happyUntil=0;playUntil=SystemClock.uptimeMillis()+12000;setState(4,850);}else if("SLEEP".equals(action)){happyUntil=0;playUntil=0;touched=SystemClock.uptimeMillis()-91000;}else if("CONFIG".equals(action)){sizeDp=prefs.getInt("size",104);int bottom=lp.y+lp.height;lp.width=dp(sizeDp);lp.height=lp.width*208/192;lp.y=bottom-lp.height;clamp();savePosition();update();}}
  if(i!=null&&"SIZE".equals(i.getAction())&&lp!=null){sizeDp=sizeDp==104?80:sizeDp==80?136:104;getSharedPreferences("pip",0).edit().putInt("size",sizeDp).apply();int bottom=lp.y+lp.height;lp.width=dp(sizeDp);lp.height=lp.width*208/192;lp.y=bottom-lp.height;clamp();ground=lp.y;savePosition();update();}
  if(extra!=null&&i!=null)extra.action(i.getAction());if(life!=null&&i!=null)life.action(i.getAction());
  return START_STICKY;
 }
 void setState(int r,int ms){row=r;frame=0;age=0;stateAge=0;limit=ms;}
 void reloadSettings(){activity=Math.max(0,Math.min(2,prefs.getInt("activity",1)));speed=Math.max(12,Math.min(100,prefs.getInt("speed",28)));quietUntil=prefs.getLong("quietUntil",0);}
 void savePosition(){if(lp==null||prefs==null)return;prefs.edit().putFloat("posX",(life!=null&&life.duo!=null&&life.duo.id!=null?life.duo.oldX:lp.x)/(float)Math.max(1,width-lp.width)).putFloat("posY",(life!=null&&life.duo!=null&&life.duo.id!=null?life.duo.oldY:lp.y)/(float)Math.max(1,height-lp.height)).apply();savedAt=SystemClock.uptimeMillis();}
 void touch(){touched=SystemClock.uptimeMillis();sleeping=false;}
 void petting(){if(life!=null)life.reward("Погладили за ушком. Хорошо рядом с тобой.");touch();happyUntil=SystemClock.uptimeMillis()+5000;setState(6,5000);}
 void clamp(){measure();lp.x=Math.max(0,Math.min(width-lp.width,lp.x));lp.y=Math.max(dp(24),Math.min(height-lp.height-dp(28),lp.y));posX=lp.x;}
 void update(){if(overlayAdded)try{wm.updateViewLayout(pet,lp);}catch(RuntimeException e){stopSelf();}}
 final Runnable loop=new Runnable(){public void run(){
  if(!screenOn||pet==null||!overlayAdded)return;long now=SystemClock.uptimeMillis();int dt=(int)Math.min(150,Math.max(1,now-last));last=now;
  if(System.currentTimeMillis()<quietUntil){pet.setVisibility(View.INVISIBLE);if(extra!=null)extra.visible(false);if(life!=null)life.visible(false);handler.postDelayed(this,500);return;}if(pet.getVisibility()!=View.VISIBLE)pet.setVisibility(View.VISIBLE);
  if(now-savedAt>=10000)savePosition();
  if(!dragging){if(life!=null&&life.tick(dt,now)){handler.postDelayed(this,40);return;}if(extra!=null&&extra.tick(dt,now)){handler.postDelayed(this,40);return;}stateAge+=dt;age+=dt;
   sleeping=now-touched>=90000;
   if(sleeping){row=0;frame=3;age=0;}else if(now<happyUntil){row=6;frame=3;age=0;}else{
   if(stateAge>=limit){if(now<playUntil){int game=random.nextInt(4);if(game==0)setState(4,850);else if(game==1)setState(3,700);else{direction=game==2?1:-1;setState(direction>0?1:2,1000);}}else{int c=random.nextInt(activity==0?18:activity==2?8:12);if(c<4||c>10)setState(0,3000+random.nextInt(4000));else if(c<7){direction=random.nextBoolean()?1:-1;setState(direction>0?1:2,1800+random.nextInt(3000));}else if(c==7)setState(4,850);else if(c==8)setState(3,700);else if(c==9)setState(6,3000);else setState(7,2200);}}
   if(row==1||row==2){posX+=direction*dp(speed)*dt/1000f;if(posX<0||posX>width-lp.width){posX=Math.max(0,Math.min(width-lp.width,posX));direction=-direction;setState(direction>0?1:2,2200);}lp.x=Math.round(posX);update();}
   while(age>=durations[row][frame]){age-=durations[row][frame];frame=(frame+1)%durations[row].length;}}
   if(row!=drawnRow||frame!=drawnFrame){pet.invalidate();drawnRow=row;drawnFrame=frame;}
  }
  handler.postDelayed(this,40);
 }};
 class PetView extends View {
  Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);Rect src=new Rect();RectF dst=new RectF();float downX,downY,lastX,lastDx;int originX,originY,reversals;long downTime,lastTap;boolean moved,stroked;
  PetView(){super(PetService.this);setContentDescription("Пип — перетащи или нажми");}
  @Override protected void onDraw(Canvas c){super.onDraw(c);if(atlas==null)return;src.set(frame*192,row*208,(frame+1)*192,(row+1)*208);dst.set(0,0,getWidth(),getHeight());c.drawBitmap(atlas,src,dst,paint);if(extra!=null)extra.drawAccessory(c,row,frame,getWidth(),getHeight());}
  @Override public boolean onTouchEvent(android.view.MotionEvent e){
   switch(e.getActionMasked()){
    case MotionEvent.ACTION_DOWN:touch();dragging=true;moved=false;stroked=false;reversals=0;lastDx=0;downX=e.getRawX();lastX=downX;downY=e.getRawY();originX=lp.x;originY=lp.y;downTime=SystemClock.uptimeMillis();return true;
    case MotionEvent.ACTION_MOVE:float dx=e.getRawX()-downX,dy=e.getRawY()-downY;float step=e.getRawX()-lastX;if(Math.abs(step)>dp(2)){if(lastDx*step<0)reversals++;lastDx=step;}lastX=e.getRawX();if(reversals>=3&&Math.abs(dy)<dp(30)&&Math.abs(dx)<dp(90)){stroked=true;lp.x=originX;lp.y=originY;clamp();update();petting();}if(Math.abs(dx)+Math.abs(dy)>dp(6))moved=true;if(moved&&!stroked){lp.x=originX+Math.round(dx);lp.y=originY+Math.round(dy);clamp();ground=lp.y;update();}return true;
    case MotionEvent.ACTION_UP:dragging=false;clamp();ground=lp.y;savePosition();update();if(extra!=null&&extra.mode==1){extra.companyX=lp.x;extra.companyY=lp.y;}long now=SystemClock.uptimeMillis();if(stroked){petting();}else if(!moved){if(now-downTime>700){startActivity(new Intent(PetService.this,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));}else if(now-lastTap<350){setState(4,850);lastTap=0;}else{setState(3,700);lastTap=now;}performClick();}else setState(0,3000);return true;
    case MotionEvent.ACTION_CANCEL:dragging=false;return true;
   }return true;
  }
  @Override public boolean performClick(){super.performClick();return true;}
 }
 @Override public void onConfigurationChanged(Configuration c){super.onConfigurationChanged(c);if(lp!=null){float x=prefs.getFloat("posX",.9f),y=prefs.getFloat("posY",.9f);measure();lp.x=Math.round(x*Math.max(0,width-lp.width));lp.y=Math.round(y*Math.max(0,height-lp.height));clamp();ground=lp.y;update();if(extra!=null)extra.reload(true);}}
 @Override public void onDestroy(){savePosition();handler.removeCallbacksAndMessages(null);try{unregisterReceiver(screenReceiver);}catch(IllegalArgumentException e){}if(life!=null)life.close();if(extra!=null)extra.close();if(overlayAdded){try{wm.removeView(pet);}catch(RuntimeException e){}overlayAdded=false;}if(atlas!=null)atlas.recycle();stopForeground(STOP_FOREGROUND_REMOVE);super.onDestroy();}
 @Override public IBinder onBind(Intent i){return null;}
 @Override protected void dump(FileDescriptor fd,PrintWriter out,String[] args){out.println("Pip version=1.4.0 row="+row+" frame="+frame+" sleeping="+sleeping+" happy="+(SystemClock.uptimeMillis()<happyUntil)+" playing="+(SystemClock.uptimeMillis()<playUntil)+" quiet="+(System.currentTimeMillis()<quietUntil)+" activity="+activity+" speed="+speed+" size="+sizeDp+" position="+(lp==null?"none":lp.x+","+lp.y)+" visible="+(pet!=null&&pet.getVisibility()==View.VISIBLE)+(extra==null?"":" mode="+extra.mode+" accessory="+extra.accessory+" reminder="+extra.reminder+" ballPhase="+extra.phase+" ballVisible="+extra.ballVisible+" homeVisible="+extra.homeVisible+" catches="+extra.catches+" returns="+extra.returns+" home="+extra.hp.x+","+extra.hp.y)+(life==null?"":" bond="+prefs.getInt("bond",0)+" level="+LifeFeatures.level(prefs)+" dance="+(SystemClock.uptimeMillis()<life.danceEnd)+" focus="+(life.focusEnd>0)+" break="+life.focusBreak+" feather="+(SystemClock.uptimeMillis()<life.featherEnd)+" guest="+(life.social.visitor()!=null)+" away="+life.social.away()+" duo="+life.duo.game+" meetings="+(life.social.activeVisit()==null?0:life.social.activeVisit().optInt("meetings"))+" shelf="+prefs.getString("giftCounts","{}").replace(" ","")));}
}
