namespace OriginalB.Platform.Interfaces
{
    public interface IStorageService
    {
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
        bool HasKey(string key);
        void Save();
    }
}
