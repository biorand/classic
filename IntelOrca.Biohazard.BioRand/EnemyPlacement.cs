namespace IntelOrca.Biohazard.BioRand
{
    public class EnemyPlacement
    {
        public RdtId RdtId { get; set; }
        public int GlobalId { get; set; }
        public int Id { get; set; }
        public int Type { get; set; }
        public int Pose { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int D { get; set; }
        public bool Create { get; set; }
        public int[] Esp { get; set; } = [];
        public string? Condition { get; set; }
    }
}
