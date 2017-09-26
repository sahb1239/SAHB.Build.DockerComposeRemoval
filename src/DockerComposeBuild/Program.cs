using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sprache;

namespace DockerComposeBuild
{
    internal class Project
    {
        public Project(Guid guid, string name, string projectFile, Guid buildConfiguration)
        {
            Guid = guid;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ProjectFile = projectFile ?? throw new ArgumentNullException(nameof(projectFile));
            BuildConfiguration = buildConfiguration;
        }

        public Guid Guid { get; set; }
        public string Name { get; set; }

        public string ProjectFile { get; set; }
        public Guid BuildConfiguration { get; set; }

        public override string ToString()
        {
            return $"{nameof(Guid)}: {Guid}, {nameof(Name)}: {Name}, {nameof(ProjectFile)}: {ProjectFile}, {nameof(BuildConfiguration)}: {BuildConfiguration}";
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: DockerComposeBuild <target> <destination>");
                return;
            }

            var target = args[0];
            var destination = args[1];

            // Read target
            var targetFile = File.ReadAllText(target);
            var targetFileMultiline = File.ReadLines(target).ToArray();

            // Parse all projects
            var projects = ReadProjects(targetFile);
            
            // Find docker compose
            var dockerComposeProjects =
                projects.Where(e => e.Name == "docker-compose" && e.ProjectFile.EndsWith(".dcproj")).ToList();

            // Check if docker compose does not exists
            if (!dockerComposeProjects.Any())
            {
                Console.WriteLine("Aborting: No docker compose project found");
                return;
            }

            // Check if contain more than one compose file
            if (dockerComposeProjects.Count > 1)
            {
                Console.WriteLine("Aborting: Multiple docker compose files found");
                return;
            }

            // Get docker compose
            var dockerCompose = dockerComposeProjects.First();

            // Update docker compose
            var destinationFileMultiline = new List<string>();
            for (int i = 0; i < targetFileMultiline.Length; i++)
            {
                if (targetFileMultiline[i].Contains(dockerCompose.Name, StringComparison.OrdinalIgnoreCase) &&
                    targetFileMultiline[i].Contains(dockerCompose.ProjectFile, StringComparison.OrdinalIgnoreCase) &&
                    targetFileMultiline[i].Contains(dockerCompose.Guid.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    targetFileMultiline[i].Contains(dockerCompose.ProjectFile.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    i += 1;
                    continue;
                }

                if (targetFileMultiline[i].Contains(dockerCompose.BuildConfiguration.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                destinationFileMultiline.Add(targetFileMultiline[i]);
            }

            // Write output
            File.WriteAllLines(destination, destinationFileMultiline);
        }

        private static IEnumerable<Project> ReadProjects(string slnFile)
        {
            // Setup parsers
            Parser<Guid> guidParser =
                from open in Parse.String("\"{")
                from guid in Parse.LetterOrDigit.Or(e => Parse.Char('-').Invoke(e)).Many()
                from close in Parse.String("}\"")
                select Guid.Parse(string.Join("", guid));

            Parser<string> nameParser =
                from open in Parse.Char('\"')
                from name in Parse.CharExcept('\"').Many()
                from close in Parse.Char('\"')
                select string.Join("", name);

            Parser<Project> projectParser =
                from beginProject in Parse.String("(")
                from guid in guidParser.Token()
                from endProject in Parse.String(")")
                from whiteSpace in Parse.WhiteSpace.Many()
                from equal in Parse.Char('=')
                from whitespace2 in Parse.WhiteSpace.Many()
                from name in nameParser.Token()
                from whitespace3 in Parse.WhiteSpace.Many()
                from comma1 in Parse.Char(',')
                from whitespace4 in Parse.WhiteSpace.Many()
                from projectFile in nameParser.Token()
                from whitespace5 in Parse.WhiteSpace.Many()
                from comma2 in Parse.Char(',')
                from whitespace6 in Parse.WhiteSpace.Many()
                from buildConfiguration in guidParser.Token()
                from whitespace7 in Parse.WhiteSpace.Many()

                select new Project(guid, name, projectFile, buildConfiguration);

            var slnParser =
                from whitespace1 in Parse.WhiteSpace.Or(Parse.AnyChar.Except(Parse.String("Project"))).Many()
                from parsedProjects in Parse.Many(
                    //   from ignored in Parse.Until((Parse.WhiteSpace.Or(Parse.AnyChar)).Many(), Parse.String("Project"))
                    from _1 in Parse.WhiteSpace.Many()
                    from beginProject in Parse.String("Project")
                    from project in projectParser.Token()
                    from endProject in Parse.String("EndProject")
                    from _2 in Parse.WhiteSpace.Many()
                    select project
                )
                from whitespace2 in (Parse.WhiteSpace.Or(Parse.AnyChar)).Many()

                select parsedProjects;

            return slnParser.Parse(slnFile);
        }
    }
}
