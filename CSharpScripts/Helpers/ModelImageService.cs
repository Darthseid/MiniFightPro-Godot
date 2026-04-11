using Godot;
using System;

public static class ModelImageService
{
    public const string DefaultPresetImagePath = "res://Assets/ModelIcons/gunman2.png";

    public static string GetCustomImagePath(Model model)
    {
        if (model == null)
            return string.Empty;

        EnsureModelIdentityAndDefault(model);
        return $"user://custom_models/{model.ModelId}.png";
    }

    public static Error ImportAndApplyCustomImage(Model model, string sourcePath, int maxDim = 1024) // maxDim is the maximum width or height for the imported image to prevent excessive memory usage
    {
        if (model == null || string.IsNullOrWhiteSpace(sourcePath))
            return Error.InvalidParameter;

        EnsureModelIdentityAndDefault(model);

        var image = new Image();
        var loadError = image.Load(sourcePath);
        if (loadError != Error.Ok)
        {
            GD.PrintErr($"Failed to load image '{sourcePath}': {loadError}");
            return loadError;
        }

        var width = image.GetWidth();
        var height = image.GetHeight();
        var largest = Math.Max(width, height);
        if (largest > maxDim)
        {
            var ratio = maxDim / (float)largest;
            var resizedWidth = Math.Max(1, Mathf.RoundToInt(width * ratio));
            var resizedHeight = Math.Max(1, Mathf.RoundToInt(height * ratio));
            image.Resize(resizedWidth, resizedHeight, Image.Interpolation.Lanczos);
        }

        DirAccess.MakeDirRecursiveAbsolute("user://custom_models");

        var destinationPath = GetCustomImagePath(model);
        var saveError = image.SavePng(destinationPath);
        if (saveError != Error.Ok)
        {
            GD.PrintErr($"Failed to save image '{destinationPath}': {saveError}");
            return saveError;
        }

        model.CustomImagePath = destinationPath;
        GameData.Instance?.SaveModelsToFile();
        return Error.Ok;
    }

    public static Texture2D LoadTextureForModel(Model model)
    {
        if (model == null)
            return null;

        EnsureModelIdentityAndDefault(model);

        if (!string.IsNullOrWhiteSpace(model.CustomImagePath) && FileAccess.FileExists(model.CustomImagePath)) // Check if the custom image path is set and the file exists before trying to load it
        {
            var image = new Image();
            var loadErr = image.Load(model.CustomImagePath);
            if (loadErr == Error.Ok)
                return ImageTexture.CreateFromImage(image);

            GD.PrintErr($"Failed to load custom model image '{model.CustomImagePath}': {loadErr}");
        }

        if (!string.IsNullOrWhiteSpace(model.DefaultImagePath)) 
            return ResourceLoader.Load<Texture2D>(model.DefaultImagePath);

        return null;
    }

    public static bool EnsureModelIdentityAndDefault(Model model)
    {
        if (model == null)
            return false;

        var changed = false;
        if (string.IsNullOrWhiteSpace(model.ModelId))
        {
            model.ModelId = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(model.DefaultImagePath))
        {
            model.DefaultImagePath = DefaultPresetImagePath;
            changed = true;
        }

        if (model.CustomImagePath == null)
        {
            model.CustomImagePath = string.Empty;
            changed = true;
        }
        return changed;
    }
}
