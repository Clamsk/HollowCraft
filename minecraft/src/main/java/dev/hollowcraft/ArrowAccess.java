package dev.hollowcraft.mixin;
import net.minecraft.entity.projectile.PersistentProjectileEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(PersistentProjectileEntity.class)
public interface ArrowAccess {
    @Accessor("inGround") boolean hollowcraft$inGround();
}
