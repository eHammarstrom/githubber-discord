﻿using System;
using Discord;
using Octokit;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Program {
    static void Main(string[] args) {
        if (args.Length != 2) throw new ArgumentException("Must provide discord token and github token in order of mention.");
        new Program().Start(args[0], args[1]);
    }

    private GitHubClient _githubClient;

    private DiscordClient _discordClient;
    private string _discordToken;

    private List<Server> _discordServers;

    public void Start(string discordToken, string githubToken) {
        _discordToken = discordToken;
        _discordClient = new DiscordClient();
        _githubClient = configureGithub(githubToken);
        _discordServers = new List<Server>();

        _discordClient.ExecuteAndWait(async () => {
            await _discordClient.Connect(_discordToken);
            Console.WriteLine("Hubber is {0}", _discordClient.Status.Value);
            Console.WriteLine("Hubber is waiting for available servers.");

            _discordClient.ServerAvailable += (s, e) => {
                _discordServers.Add(e.Server);

                Server server = DiscordServerSelection();
                Channel channel = DiscordChannelSelection(server);

                GithubRepositorySelection((repo) => {
                    new Task(() => this.HandleCommits(server, channel, repo)).Start();
                });
            };
        });
    }

    #region CLI Quiz
    void PrintDiscordServer() {
        int i = 0;
        Console.WriteLine("Select a server: ");
        _discordServers.ForEach(x => Console.WriteLine(i++ + ": " + x.Name));
    }

    Server DiscordServerSelection() {
        int selection;
        PrintDiscordServer();

        if (int.TryParse(Console.ReadLine(), out selection) && _discordServers.Count > selection)
            return _discordServers[selection];
        else
            return DiscordServerSelection();
    }

    void PrintDiscordChannels(Server server) {
        int i = 0;
        Console.WriteLine("Select a channel: ");
        server.TextChannels.ToList().ForEach(x => Console.WriteLine(i++ + ": " + x.Name));
    }

    Channel DiscordChannelSelection(Server server) {
        int selection;
        PrintDiscordChannels(server);
        List<Channel> channels = server.TextChannels.ToList();

        if (int.TryParse(Console.ReadLine(), out selection) && channels.Count > selection)
            return channels[selection];
        else
            return DiscordChannelSelection(server);
    }

    async void PrintGithubRepositories() {
        int i = 0;
        Console.WriteLine("Retrieving repositories... ");
        var repos = await _githubClient.Repository.GetAllForCurrent();
        Console.WriteLine("Select a repo: ");
        repos.ToList().ForEach(x => Console.WriteLine(i++ + ": " + x.Name));
    }

    async void GithubRepositorySelection(Action<Repository> callback) {
        int selection;
        PrintGithubRepositories();
        List<Repository> repos = (await _githubClient.Repository.GetAllForCurrent()).ToList();

        if (callback != null && int.TryParse(Console.ReadLine(), out selection) && repos.Count > selection)
            callback(repos[selection]);
        else
            GithubRepositorySelection(callback);

        //if (callback != null && repo != null) callback(repo);
        //else GithubRepositorySelection(callback);
    }
    #endregion

    GitHubClient configureGithub(string token) {
        _githubClient = new GitHubClient(new ProductHeaderValue("githubber-discord"));
        _githubClient.Credentials = new Credentials(token);
        return _githubClient;
    }

    async void HandleCommits(Server server, Channel channel, Repository repo) {
        Console.WriteLine("Hubber Jr. is handling {0}:{1} with care.", server.Name, channel.Name);
        Console.WriteLine("And Hubber Jr. listens to {0}@github.", repo.Name);
        int numOfCommits = (await _githubClient.Repository.Commit.GetAll(repo.Id)).Count;

        while (true) {
            var commits = await _githubClient.Repository.Commit.GetAll(repo.Id);

            if (commits.Count > numOfCommits) {
                int take = commits.Count - numOfCommits;
                var toAnnounce = commits.Take(take);

                toAnnounce.ToList().ForEach(async x => {
                    string msg = x.Author.Login + " has committed changes.\n" + x.HtmlUrl;
                    await channel.SendMessage(msg);
                });

                numOfCommits = commits.Count;
            }
            await Task.Delay(1 * 1000);
        }
    }
}