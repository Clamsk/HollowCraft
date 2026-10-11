package dev.hollowcraft;

/** Bridged server actions have no matching vanilla client prediction. */
public final class RemoteEffects implements AutoCloseable {
    private static final ThreadLocal<Integer> depth=ThreadLocal.withInitial(()->0);
    private boolean closed;
    private RemoteEffects() {depth.set(depth.get()+1);}
    public static RemoteEffects begin() {return new RemoteEffects();}
    public static boolean active() {return depth.get()>0;}
    @Override public void close() {
        if(closed)return;closed=true;int value=depth.get()-1;if(value<=0)depth.remove();else depth.set(value);
    }
}
