import { exec } from 'child_process';
import type { Plugin } from 'vite';

interface RunBeforeBuildPluginOptions {
    command?: string;
    args?: string[];
    cwd?: string;
}

/**
 * Vite plugin to run a shell command before build starts.
 *
 * @param options Configuration options for the command.
 * @returns Vite plugin object.
 */
export default function runBeforeBuildPlugin(
    options: RunBeforeBuildPluginOptions = {}
): Plugin {
    const command = options.command ?? 'npm';
    const args = options.args ?? ['run', 'gen:lib'];
    const cwd = options.cwd ?? process.cwd();

    return {
        name: 'run-before-build',
        apply: 'build',
        async buildStart() {
            const fullCommand = `${command} ${args.join(' ')}`;
            console.log(`[run-before-build] Running command: ${fullCommand}`);

            return new Promise<void>((resolve, reject) => {
                const child = exec(
                    fullCommand,
                    { cwd },
                    (error, stdout, stderr) => {
                        if (error) {
                            console.error(`[run-before-build] Error:`, error);
                            reject(error);
                            return;
                        }
                        //if (stdout) console.log(`[run-before-build] stdout:\n${stdout}`);
                        if (stderr)
                            console.error(
                                `[run-before-build] stderr:\n${stderr}`
                            );
                        resolve();
                    }
                );

                child.stdout?.pipe(process.stdout);
                child.stderr?.pipe(process.stderr);
            });
        },
    };
}
