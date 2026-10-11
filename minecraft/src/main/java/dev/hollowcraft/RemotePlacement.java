package dev.hollowcraft;

import java.util.function.Function;
import net.minecraft.item.ItemUsageContext;
import net.minecraft.server.network.ServerPlayerEntity;
import net.minecraft.util.ActionResult;
import net.minecraft.util.Hand;

/** Redirect only this remote click's BlockItem use to its checked 2D placement cell. */
public final class RemotePlacement implements AutoCloseable {
    private static final ThreadLocal<RemotePlacement> current=new ThreadLocal<>();
    private final RemotePlacement previous;
    private final ServerPlayerEntity player;
    private final Function<Hand,ActionResult> placement;
    private boolean invoking;
    private RemotePlacement(ServerPlayerEntity player,Function<Hand,ActionResult> placement) {
        this.player=player;this.placement=placement;previous=current.get();current.set(this);
    }
    static RemotePlacement begin(ServerPlayerEntity player,Function<Hand,ActionResult> placement) {return new RemotePlacement(player,placement);}
    public static ActionResult apply(ItemUsageContext context) {
        var scope=current.get();
        if(scope==null||scope.invoking||context.getPlayer()!=scope.player)return null;
        scope.invoking=true;
        try {return scope.placement.apply(context.getHand());}
        finally {scope.invoking=false;}
    }
    @Override public void close() {if(previous==null)current.remove();else current.set(previous);}
}
