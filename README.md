# Unity package: SK.Libretro
Libretro wrapper written in C# with support for the Unity game engine

## RetroCVR building specifics
1. Update the `PostBuildEvent` in `Directory.Build.props` to point to the `Kafe_CVR_Mods\.ManagedLibs\` directory
2. Fix all paths for the `.dlls` in `SK.Libretro.csproj`,  `SK.Libretro.NAudio.csproj`, and  `SK.Libretro.Unity.csproj`
3. Build solution

- Usage example: https://github.com/Skurdt/LibretroUnityFE

- Video: Super Mario 64 (Mupen64PlusNext):  
[![Alt text](https://img.youtube.com/vi/euec6832wNA/0.jpg)](https://youtu.be/euec6832wNA)

- Video: Mupen64PlusNext, PPSSPP and mame:  
[![Alt text](https://img.youtube.com/vi/YOrZ2_0IcLI/0.jpg)](https://youtu.be/YOrZ2_0IcLI)
