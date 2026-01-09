function Main() {
    //GVariables.DebugMode = false;
    //可在此处自定义，以避免重新编译的麻烦
    //废弃，请使用EngineInit.cs
    Console.WriteLine("Running");
    Console.WriteLine(GVariables.OS);
    Console.WriteLine(OSt.Windows);
    if (GVariables.OS == OSt.Windows) {
        Console.WriteLine("Into the Space of Windows");
        GVariables.GraphicsAPI = GraphicsAPIt.OpenGL2D;
        //Console.WriteLine("Change To Vulkan");
    }
    //Console.WriteLine("Change OS to the Linux");
    //GVariables.OS = OSt.Linux;
    Console.WriteLine("InProgramInfo");
    
    return 0;
}