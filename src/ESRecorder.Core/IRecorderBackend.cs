namespace ESRecorder.Core;

public interface IRecorderBackend
{
    int NativeLibraryVersion { get; }

    void Initialise(int instanceId);

    void Compile(int instanceId, string engineScriptPath);

    EngineMetadata GetEngineMetadata(int instanceId);

    SampleMeasurement Record(int instanceId, RecordingSample sample);

    RecorderStatus GetStatus(int instanceId);
}
