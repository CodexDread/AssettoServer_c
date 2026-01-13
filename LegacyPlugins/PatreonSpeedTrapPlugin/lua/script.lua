local pluginPath = ""
local configuration = {}

-- Called when the plugin is loaded
function initialize(cfg, path)
    configuration = cfg
    pluginPath = path
end

local function generateOverlay(metadata)
    local overlayImage = MagickImage(pluginPath .. "/lua/overlay-scaled.png")

    local draw = Drawables()
    :Font(pluginPath .. "/e1234/E1234-Italic.ttf")
    :EnableTextAntialias()
    :StrokeColor(MagickColors.White)
    :FillColor(MagickColors.White)
    :FontPointSize(40)
    :TextAlignment(TextAlignment.Left)
    :Text(80, 106, tostring(metadata.ActualSpeed))
    :FontPointSize(22)
    :TextAlignment(TextAlignment.Right)
    :Text(164, 27, string.format("%03d", configuration.NumberOffset + metadata.SpeedTrapId))
    :TextAlignment(TextAlignment.Left)
    :Text(192, 27, string.format("%03d", metadata.Counter))
    :Text(80, 142, metadata.Timestamp:ToString("dd.MM.yy"))
    :TextAlignment(TextAlignment.Right)
    :Text(123, 181, metadata.Timestamp:ToString("HH"))
    :TextAlignment(TextAlignment.Left)
    :Text(150, 181, metadata.Timestamp:ToString("mm"))
    :Text(210, 106, tostring(metadata.AllowedSpeed))
    :TextAlignment(TextAlignment.Right)
    :Text(243, 218, tostring(metadata.Lane))
    :Draw(overlayImage);

    return overlayImage;
end

-- Called to process an image
-- image is of C# type MagickImage
-- metadata contains info about the image: SpeedTrapId, ActualSpeed, AllowedSpeed, Lane, Counter, Timestamp, Name, Guid, Car
function processImage(image, metadata)
    if configuration.Grayscale then
        image:Grayscale()
    end
    
    if configuration.EnableOverlay then
        local overlayImage = generateOverlay(metadata)
        image:Composite(overlayImage, 800 - overlayImage.Width, 0, CompositeOperator.Atop);
    end
end