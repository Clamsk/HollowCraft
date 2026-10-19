import com.google.gson.*;
import com.mojang.serialization.JsonOps;
import net.minecraft.SharedConstants;
import net.minecraft.Bootstrap;
import net.minecraft.registry.Registries;
import net.minecraft.village.TradeOffers;
import net.minecraft.village.TradeOffer;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NbtOps;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;

/** Offline factory sampling. Never creates a world, server or client. */
public final class NativeTradeExporter {
    static JsonObject stack(ItemStack stack) {
        JsonObject value=new JsonObject();value.addProperty("id",Registries.ITEM.getId(stack.getItem()).toString());value.addProperty("count",stack.getCount());value.addProperty("damage",stack.getDamage());value.add("nbt",stack.getNbt()==null?new JsonObject():NbtOps.INSTANCE.convertTo(JsonOps.INSTANCE,stack.getNbt()));return value;
    }
    public static void main(String[] args)throws Exception {
        SharedConstants.createGameVersion();Bootstrap.initialize();JsonArray rows=new JsonArray(),skipped=new JsonArray();
        for(var profession:TradeOffers.PROFESSION_TO_LEVELED_TRADE.entrySet())for(var level:profession.getValue().int2ObjectEntrySet()) {
            int index=0;for(var factory:level.getValue()){boolean found=false;String reason="";
                for(int sample=0;sample<4;sample++)try{TradeOffer offer=factory.create(null,net.minecraft.util.math.random.Random.create(8191L+sample*199L));if(offer==null)continue;JsonObject row=new JsonObject();row.addProperty("profession",Registries.VILLAGER_PROFESSION.getId(profession.getKey()).toString());row.addProperty("level",level.getIntKey());row.addProperty("factory",index);row.addProperty("sample",sample);row.add("buy",stack(offer.getOriginalFirstBuyItem()));row.add("second",stack(offer.getSecondBuyItem()));row.add("sell",stack(offer.getSellItem()));row.addProperty("maxUses",offer.getMaxUses());row.addProperty("experience",offer.getMerchantExperience());row.addProperty("multiplier",offer.getPriceMultiplier());rows.add(row);found=true;}catch(RuntimeException ex){reason=ex.getClass().getSimpleName();}
                if(!found)skipped.add(Registries.VILLAGER_PROFESSION.getId(profession.getKey())+"/"+level.getIntKey()+"/"+index+"/"+reason);index++;
            }
        }
        JsonObject result=new JsonObject();result.addProperty("schema",1);result.addProperty("minecraft","1.20.1");result.addProperty("source","Numerical trade factory samples; factories requiring a live world are explicitly excluded.");result.add("trades",rows);result.add("excluded",skipped);Files.writeString(Path.of(args[0]),new Gson().toJson(result),StandardCharsets.UTF_8);System.out.println("Trade samples: "+rows.size()+"; factories needing live entity/world context: "+skipped.size());
    }
}
