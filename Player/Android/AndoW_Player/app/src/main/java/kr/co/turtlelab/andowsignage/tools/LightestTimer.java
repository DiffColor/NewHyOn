package kr.co.turtlelab.andowsignage.tools;

import android.os.Handler;

public class LightestTimer {
	  
	long _interval; 
	long getInterval() { return _interval; } 
	void setInterval(int delay) { _interval = delay; } 
	Handler handler; 
	Runnable _tickHandler; 
	Runnable delegate; 
	boolean ticking; 
	  
	public boolean getIsTicking(){ return ticking; } 
		  
	public LightestTimer(int interval) 
	{ 
		_interval = interval; 
		handler = new Handler(); 
	}
	
	public LightestTimer(Runnable onTickHandler) {
		setOnTickHandler(onTickHandler); 
		handler = new Handler(); 
	}
	 
	public LightestTimer(int interval, Runnable onTickHandler) 
	{ 
		_interval = interval; 
		setOnTickHandler(onTickHandler); 
		handler = new Handler(); 
	} 
	  
	public void start(int interval, Runnable onTickHandler) 
	{ 
		if (ticking) return; 
		
		_interval = interval; 
		setOnTickHandler(onTickHandler); 
		handler.postDelayed(delegate, _interval); 
		ticking = true; 
	} 
	  
	public void start() 
	{ 
		if (ticking) return; 
		
		handler.postDelayed(delegate, _interval); 
		ticking = true; 
	} 
	  
	public void stop() 
	{ 
		handler.removeCallbacks(delegate); 
	    //handler.removeCallbacksAndMessages(null);
	    ticking = false; 
	} 
	
	public void changeInterval(long interval) {
		handler.removeCallbacks(delegate);
	    //handler.removeCallbacksAndMessages(null);
		_interval = interval;
		handler.postDelayed(delegate, _interval);
	}
	  
	public void setOnTickHandler(Runnable onTickHandler) 
	{ 
	    if (onTickHandler == null) return; 
	    
	    _tickHandler = onTickHandler; 
	    
	    delegate = new Runnable() { 
	    	public void run() 
	    	{ 
	    		if (_tickHandler == null) return; 
	    		_tickHandler.run(); 
	    		handler.postDelayed(delegate, _interval); 
	    	} 
	    }; 
	}
}
