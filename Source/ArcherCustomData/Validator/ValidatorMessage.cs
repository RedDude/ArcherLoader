namespace ArcherLoaderMod
{
    public class ValidatorMessage
    {
        public string message;
        public ValidatorMessageType type = ValidatorMessageType.ERROR;

        public ValidatorMessage(string message)
        {
            this.message = message;
        }
    }
}