local drawableSprite = require "structs.drawable_sprite"

local customHeart = {}

customHeart.name = "vitellary/customheartt"
customHeart.placements = {
    {
        name = "custom_heartt",
        data = {
            respawnTime = 3,
			dashCount = 1,
			staminaE = 0,
			smallHitbox = false,
            type = "Blue",
            path = "CrystallineHelper/FLCC/heartGemColorable",
            color = "ff4fed",
            light = true,
			dust = true,
			static = false,
            bully = false,
            additionalEffects = true,
            switchCoreMode = false,
            colorGrade = false,
			slowdown = false,
            poemCutscene = false,
			poemId = "",
        }
    }
}
customHeart.fieldInformation = {
    type = {
        editable = false,
        options = {
            "Blue",
            "Red",
            "Gold",
            "Custom",
            "Core",
            "CoreInverted",
            "Random",
        },
    },
    dashCount = {
        fieldType = "integer",
    },
	staminaE = {
        fieldType = "integer",
    },
    color = {
        fieldType = "color",
    },
}

local function getSprite(entity)
    if entity.type == "Blue" then
        return "collectables/heartGem/0/00"
    elseif entity.type == "Red" then
        return "collectables/heartGem/1/00"
    elseif entity.type == "Gold" then
        return "collectables/heartGem/2/00"
    elseif entity.type == "Custom" then
        return "collectables/"..(entity.path == "heartGemColorable" ? "CrystallineHelper/FLCC/heartGemColorable" : entity.path).."/00"
    elseif entity.type == "Core" then
        return "CrystallineHelper/FLCC/ahorn_customcoreheart"
    elseif entity.type == "CoreInverted" then
        return "CrystallineHelper/FLCC/ahorn_customcoreheartinverted"
    else
        return "collectables/heartGem/0/00"
    end
end

function customHeart.sprite(room, entity)
    local sprite = drawableSprite.fromTexture(getSprite(entity), {x = entity.x, y = entity.y})
    if entity.type == "Custom" then
        sprite:setColor(entity.color)
    end
    return sprite
end

return customHeart