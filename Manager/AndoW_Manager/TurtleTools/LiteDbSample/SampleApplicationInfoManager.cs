using LiteDB;

namespace TurtleTools.LiteDbSample
{
    /// <summary>
    /// LiteDB 예시용 매니저. 실제 앱 로직에서는 사용되지 않습니다.
    /// </summary>
    public class SampleApplicationInfoManager : LiteDbSampleManagerBase<SampleApplicationInfoClass>
    {
        public SampleApplicationInfoClass Data { get; private set; }

        public SampleApplicationInfoManager()
            : base(nameof(SampleApplicationInfoClass), nameof(SampleApplicationInfoClass.Id))
        {
        }

        public SampleApplicationInfoClass Load()
        {
            Data = FindOne(_ => true);
            if (Data == null)
            {
                Data = new SampleApplicationInfoClass();
                Upsert(Data);
            }

            return Data;
        }

        public void Save(SampleApplicationInfoClass data)
        {
            if (data == null)
            {
                return;
            }

            Data = data;
            Upsert(Data);
        }
    }

    public class SampleApplicationInfoClass
    {
        [BsonId]
        public int Id { get; set; } = 0;
        public string SampleManagerIp { get; set; } = "127.0.0.1";
        public bool SampleFlag { get; set; } = false;
    }
}
