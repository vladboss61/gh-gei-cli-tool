namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ITargetGithubApiFactory
    {
        GithubApi Create(string apiUrl = null, string targetPersonalAccessToken = null);
    }
}
