package dev.hollowcraft.mixin;

import net.minecraft.item.BlockItem;
import net.minecraft.item.ItemStack;
import net.minecraft.item.ItemUsageContext;
import net.minecraft.util.ActionResult;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(ItemStack.class)
public abstract class RemotePlacementMixin {
    @Inject(method="useOnBlock",at=@At("HEAD"),cancellable=true)
    private void hollowcraft$checkedPlacement(ItemUsageContext context,CallbackInfoReturnable<ActionResult> callback) {
        if(((ItemStack)(Object)this).getItem() instanceof BlockItem) {
            var result=dev.hollowcraft.RemotePlacement.apply(context);
            if(result!=null)callback.setReturnValue(result);
        }
    }
}
