package dev.hollowcraft.mixin;

import net.minecraft.entity.projectile.FireworkRocketEntity;
import net.minecraft.util.hit.BlockHitResult;
import net.minecraft.world.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;

@Mixin(FireworkRocketEntity.class)
public abstract class FireworkTerrainMixin {
    @Redirect(method="explode",at=@At(value="INVOKE",target="Lnet/minecraft/world/World;raycast(Lnet/minecraft/world/RaycastContext;)Lnet/minecraft/util/hit/BlockHitResult;"))
    private BlockHitResult hollowcraft$blastVisibility(World world,RaycastContext context) {
        try(var ignored=dev.hollowcraft.NativeTerrainQueries.forceSolid()) {return world.raycast(context);}
    }
}
