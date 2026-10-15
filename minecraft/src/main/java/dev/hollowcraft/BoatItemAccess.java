package dev.hollowcraft.mixin;
import net.minecraft.item.BoatItem;
import net.minecraft.entity.vehicle.BoatEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(BoatItem.class)
public interface BoatItemAccess {
    @Accessor("type") BoatEntity.Type hollowcraft$type();
    @Accessor("chest") boolean hollowcraft$chest();
}
