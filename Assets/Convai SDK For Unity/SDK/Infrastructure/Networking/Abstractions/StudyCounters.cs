namespace Convai.Infrastructure.Networking
{
    public static class StudyCounters
    {
        public static int NumQuestions { get; private set; }
        public static int NumInteractions { get; private set; }

        public static void Reset()
        {
            NumQuestions = 0;
            NumInteractions = 0;
        }

        public static void AddQuestion()
        {
            NumQuestions++;
        }

        public static void AddInteraction()
        {
            NumInteractions++;
        }
    }
}