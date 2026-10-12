package dev.hollowcraft;

import net.minecraft.client.MinecraftClient;
import net.minecraft.util.math.Vec3d;

/** Anchor client-owned sound and particle emitters to the native player's real position. */
public final class NativePlayerEffects {
    public static void sync(MinecraftClient client) {
        var state=PlayerRenderState.latest;var input=PlayerRenderState.controls;
        if(client.player==null||state==null||input==null||!HollowCraftBridge.remoteActive()
            ||!input.has("visit")||input.get("visit").getAsInt()!=state.visit
            ||!input.has("scene")||!state.scene.equals(input.get("scene").getAsString())
            ||!input.has("campaign")||!state.campaign.equals(input.get("campaign").getAsString()))return;
        double x=state.offset+state.x,y=64+state.y;
        client.player.setPosition(x,y,.5);
        client.player.prevX=x;client.player.prevY=y;client.player.prevZ=.5;
        client.player.setVelocity(Vec3d.ZERO);client.player.fallDistance=0;
    }
}
