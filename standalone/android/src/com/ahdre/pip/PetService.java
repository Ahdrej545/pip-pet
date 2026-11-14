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
 WindowManager wm;WindowManager.LayoutParams lp;PetView pet;Bitmap atlas;Handler handler=new Handler(Looper.getMainLooper());Random random=new Random();
 boolean screenOn=true,dragging=false;int row=0,frame=0,age=0,stateAge=0,limit=4000,direction=1,width,height,ground;
 long last;int sizeDp=104;float posX; boolean overlayAdded=false;int drawnRow=-1,drawnFrame=-1;
 final int[][] durations={{280,110,110,140,140,320},{120,120,120,120,120,120,120,220},{120,120,120,120,120,120,120,220},{140,140,140,280},{140,140,140,140,280},{140,140,140,140,140,140,140,240},{150,150,150,150,150,260},{120,120,120,120,120,220},{150,150,150,150,150,260}};
 final BroadcastReceiver screenReceiver=new BroadcastReceiver(){public void onReceive(Context c,Intent i){String a=i.getAction();if(Intent.ACTION_SCREEN_OFF.equals(a)){screenOn=false;handler.removeCallbacks(loop);if(pet!=null)pet.setVisibility(View.INVISIBLE);}else if(Intent.ACTION_USER_PRESENT.equals(a)||Intent.ACTION_SCREEN_ON.equals(a)){screenOn=true;KeyguardManager k=(KeyguardManager)getSystemService(KEYGUARD_SERVICE);if(k==null||!k.isKeyguardLocked()){if(pet!=null)pet.setVisibility(View.VISIBLE);last=SystemClock.uptimeMillis();handler.removeCallbacks(loop);handler.post(loop);}}}};
 @Override public void onCreate(){super.onCreate();
  NotificationManager n=(NotificationManager)getSystemService(NOTIFICATION_SERVICE);NotificationChannel channel=new NotificationChannel("pip_pet","Пип на экране",NotificationManager.IMPORTANCE_LOW);channel.setSound(null,null);n.createNotificationChannel(channel);
  Intent settings=new Intent(this,MainActivity.class);PendingIntent open=PendingIntent.getActivity(this,1,settings,PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
  PendingIntent stop=PendingIntent.getService(this,2,new Intent(this,PetService.class).setAction("STOP"),PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
  Notification notification=new Notification.Builder(this,"pip_pet").setSmallIcon(android.R.drawable.ic_menu_view).setContentTitle("Пип рядом 🦊").setContentText("Перетаскивай, нажимай и играй с лисёнком").setOngoing(true).setContentIntent(open).addAction(new Notification.Action.Builder(null,"Спрятать",stop).build()).addAction(new Notification.Action.Builder(null,"Настройки",open).build()).build();
  if(Build.VERSION.SDK_INT>=34)startForeground(17,notification,ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);else startForeground(17,notification);
  if(!Settings.canDrawOverlays(this)){stopSelf();return;}
  try{atlas=BitmapFactory.decodeStream(getAssets().open("spritesheet.png"));}catch(IOException e){stopSelf();return;}
  wm=(WindowManager)getSystemService(WINDOW_SERVICE);sizeDp=getSharedPreferences("pip",0).getInt("size",104);measure();
  lp=new WindowManager.LayoutParams(dp(sizeDp),dp(sizeDp)*208/192,WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE|WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,PixelFormat.TRANSLUCENT);
  lp.gravity=Gravity.TOP|Gravity.LEFT;lp.x=Math.max(0,width-lp.width-dp(20));lp.y=Math.max(dp(32),height-lp.height-dp(84));posX=lp.x;ground=lp.y;
  pet=new PetView();
  try{wm.addView(pet,lp);overlayAdded=true;}catch(RuntimeException e){android.util.Log.e("Pip","Cannot create overlay",e);stopSelf();return;}
  IntentFilter f=new IntentFilter();f.addAction(Intent.ACTION_SCREEN_OFF);f.addAction(Intent.ACTION_SCREEN_ON);f.addAction(Intent.ACTION_USER_PRESENT);if(Build.VERSION.SDK_INT>=33)registerReceiver(screenReceiver,f,Context.RECEIVER_NOT_EXPORTED);else registerReceiver(screenReceiver,f);
  PowerManager power=(PowerManager)getSystemService(POWER_SERVICE);KeyguardManager lock=(KeyguardManager)getSystemService(KEYGUARD_SERVICE);screenOn=power.isInteractive()&&!lock.isKeyguardLocked();if(!screenOn)pet.setVisibility(View.INVISIBLE);
  last=SystemClock.uptimeMillis();if(screenOn)handler.post(loop);
 }
 int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
 void measure(){Rect b=wm==null?null:wm.getCurrentWindowMetrics().getBounds();if(b!=null){width=b.width();height=b.height();}else{width=getResources().getDisplayMetrics().widthPixels;height=getResources().getDisplayMetrics().heightPixels;}}
 @Override public int onStartCommand(Intent i,int flags,int startId){
  if(i!=null&&"STOP".equals(i.getAction())){getSharedPreferences("pip",0).edit().putBoolean("enabled",false).apply();stopSelf();return START_NOT_STICKY;}
  if(i!=null&&"SIZE".equals(i.getAction())&&lp!=null){sizeDp=sizeDp==104?80:sizeDp==80?136:104;getSharedPreferences("pip",0).edit().putInt("size",sizeDp).apply();int bottom=lp.y+lp.height;lp.width=dp(sizeDp);lp.height=lp.width*208/192;lp.y=bottom-lp.height;clamp();ground=lp.y;update();}
  return START_STICKY;
 }
 void setState(int r,int ms){row=r;frame=0;age=0;stateAge=0;limit=ms;}
 void clamp(){measure();lp.x=Math.max(0,Math.min(width-lp.width,lp.x));lp.y=Math.max(dp(24),Math.min(height-lp.height-dp(28),lp.y));posX=lp.x;}
 void update(){if(overlayAdded)try{wm.updateViewLayout(pet,lp);}catch(RuntimeException e){stopSelf();}}
 final Runnable loop=new Runnable(){public void run(){
  if(!screenOn||pet==null||!overlayAdded)return;long now=SystemClock.uptimeMillis();int dt=(int)Math.min(150,Math.max(1,now-last));last=now;
  if(!dragging){stateAge+=dt;age+=dt;
   if(stateAge>=limit){int c=random.nextInt(10);if(c<5)setState(0,3000+random.nextInt(4000));else if(c<8){direction=random.nextBoolean()?1:-1;setState(direction>0?1:2,1800+random.nextInt(3000));}else if(c==8)setState(4,850);else setState(3,700);}
   if(row==1||row==2){posX+=direction*dp(28)*dt/1000f;if(posX<0||posX>width-lp.width){posX=Math.max(0,Math.min(width-lp.width,posX));direction=-direction;setState(direction>0?1:2,2200);}lp.x=Math.round(posX);update();}
   while(age>=durations[row][frame]){age-=durations[row][frame];frame=(frame+1)%durations[row].length;}
   if(row!=drawnRow||frame!=drawnFrame){pet.invalidate();drawnRow=row;drawnFrame=frame;}
  }
  handler.postDelayed(this,40);
 }};
 class PetView extends View {
  Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);Rect src=new Rect();RectF dst=new RectF();float downX,downY;int originX,originY;long downTime,lastTap;boolean moved;
  PetView(){super(PetService.this);setContentDescription("Пип — перетащи или нажми");}
  @Override protected void onDraw(Canvas c){super.onDraw(c);if(atlas==null)return;src.set(frame*192,row*208,(frame+1)*192,(row+1)*208);dst.set(0,0,getWidth(),getHeight());c.drawBitmap(atlas,src,dst,paint);}
  @Override public boolean onTouchEvent(android.view.MotionEvent e){
   switch(e.getActionMasked()){
    case MotionEvent.ACTION_DOWN:dragging=true;moved=false;downX=e.getRawX();downY=e.getRawY();originX=lp.x;originY=lp.y;downTime=SystemClock.uptimeMillis();return true;
    case MotionEvent.ACTION_MOVE:float dx=e.getRawX()-downX,dy=e.getRawY()-downY;if(Math.abs(dx)+Math.abs(dy)>dp(6))moved=true;if(moved){lp.x=originX+Math.round(dx);lp.y=originY+Math.round(dy);clamp();ground=lp.y;update();}return true;
    case MotionEvent.ACTION_UP:dragging=false;clamp();ground=lp.y;update();long now=SystemClock.uptimeMillis();if(!moved){if(now-downTime>700){startActivity(new Intent(PetService.this,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));}else if(now-lastTap<350){setState(4,850);lastTap=0;}else{setState(3,700);lastTap=now;}performClick();}else setState(0,3000);return true;
    case MotionEvent.ACTION_CANCEL:dragging=false;return true;
   }return true;
  }
  @Override public boolean performClick(){super.performClick();return true;}
 }
 @Override public void onConfigurationChanged(Configuration c){super.onConfigurationChanged(c);if(lp!=null){clamp();ground=lp.y;update();}}
 @Override public void onDestroy(){handler.removeCallbacksAndMessages(null);try{unregisterReceiver(screenReceiver);}catch(IllegalArgumentException e){}if(overlayAdded){try{wm.removeView(pet);}catch(RuntimeException e){}overlayAdded=false;}if(atlas!=null)atlas.recycle();stopForeground(STOP_FOREGROUND_REMOVE);super.onDestroy();}
 @Override public IBinder onBind(Intent i){return null;}
}
