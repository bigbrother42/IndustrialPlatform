using System.Collections.Generic;

namespace Industrial.Contracts.Configuration
{
    /// <summary>
    /// 配置读写抽象：屏蔽底层存储（app.config / JSON / 数据库）。
    /// </summary>
    public interface IConfigurationProvider
    {
        T Get<T>(string key, T defaultValue = default);

        void Set<T>(string key, T value);

        bool Contains(string key);

        IReadOnlyDictionary<string, object> GetSection(string sectionName);

        void Reload();
    }
}
