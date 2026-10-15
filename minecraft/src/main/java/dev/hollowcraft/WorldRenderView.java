package dev.hollowcraft;

import com.google.gson.JsonObject;

/** Native world rectangle, independent of MC's hidden window and its camera setup. */
record WorldRenderView(float x,float y,float width,float height) {
    static WorldRenderView read(JsonObject input,double fallbackX,double fallbackY) {
        float x=value(input,"viewX",(float)fallbackX),y=value(input,"viewY",(float)fallbackY);
        float width=value(input,"viewWidth",36),height=value(input,"viewHeight",24);
        if(!Float.isFinite(x)||Math.abs(x)>500)x=(float)fallbackX;
        if(!Float.isFinite(y)||y<-100||y>300)y=(float)fallbackY;
        if(!Float.isFinite(width)||width<2||width>120)width=36;
        if(!Float.isFinite(height)||height<2||height>80)height=24;
        return new WorldRenderView(x,y,width,height);
    }
    private static float value(JsonObject input,String key,float fallback) {
        try {return input.has(key)?input.get(key).getAsFloat():fallback;}catch(RuntimeException ignored) {return fallback;}
    }
}
