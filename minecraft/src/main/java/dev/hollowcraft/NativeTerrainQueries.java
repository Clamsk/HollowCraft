package dev.hollowcraft;

/** Flight raycasts defer to HK; damage visibility raycasts still need solid native terrain. */
public final class NativeTerrainQueries implements AutoCloseable {
    private static final ThreadLocal<Integer> depth=ThreadLocal.withInitial(()->0);
    private final int previous;
    private NativeTerrainQueries() {previous=depth.get();depth.set(previous+1);}
    public static NativeTerrainQueries forceSolid() {return new NativeTerrainQueries();}
    public static boolean solid() {return depth.get()>0;}
    public void close() {if(previous==0)depth.remove();else depth.set(previous);}
}
