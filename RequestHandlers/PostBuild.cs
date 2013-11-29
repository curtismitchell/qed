﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using fn = qed.Functions;

namespace qed
{
    public static partial class Handlers
    {
        public static async Task PostBuild(
            IDictionary<string, object> environment,
            dynamic @params,
            Func<IDictionary<string, object>, Task> next)
        {
            var owner = @params.owner as string;
            var name = @params.name as string;

            var buildConfiguration = fn.GetBuildConfiguration(owner, name);
            if (buildConfiguration == null)
            {
                environment.SetStatusCode(400);
                return;
            }

            var fail = new Func<int, string, Task>(async (statusCode, errorMessage) =>
            {
                environment.SetStatusCode(statusCode);

                var responseModel = new
                {
                    owner = buildConfiguration.Owner,
                    name = buildConfiguration.Name,
                    builds = CreateBuildsResponseModel(buildConfiguration),
                    errorMessage
                };

                await environment.Render(
                    "builds",
                    responseModel);
            });

            var form = await environment.ReadFormAsync();

            var branchOrPr = form["branch-or-pr"].FirstOrDefault();

            if (String.IsNullOrEmpty(branchOrPr))
            {
                await fail(400, "A branch or pull request number is required.");
                return;
            }

            string @ref;
            if (branchOrPr.StartsWith("#"))
                @ref = String.Concat("refs/pull/", branchOrPr.Substring(1), "/head");
            else
                @ref = String.Concat("refs/heads/", branchOrPr);

            var newBuild = fn.CreateBuild(
                buildConfiguration.Command,
                buildConfiguration.CommandArguments,
                name,
                owner,
                @ref,
                null,
                null,
                null);

            var location = String.Format(
                "/{0}/{1}/builds/{2}",
                newBuild.RepositoryOwner,
                newBuild.RepositoryName,
                newBuild.Id);

            var absoluteLocation = String.Format(
                "http://{0}{1}",
                await fn.GetHost(),
                location);

            environment.SetStatusCode(201);
            var responseHeaders = environment.GetResponseHeaders();
            responseHeaders.Add("Location", new [] { absoluteLocation });
            await environment.Render("queued", new { id = newBuild.Id, location});
        }
    }
}
