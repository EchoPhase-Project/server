import fs from 'fs';
import path from 'path';

function toPascalCase(str: string) {
    return str.replace(/(^\w|-\w)/g, (m) => m.replace('-', '').toUpperCase());
}

const ROOT_DIR = path.resolve(__dirname, '../src/lib');
const excludedRelativePaths = new Set<string>([
    'i18n/locales',
    '__tests__',
    '.DS_Store',
]);

function isExcluded(fullPath: string): boolean {
    const relative = path.relative(ROOT_DIR, fullPath);
    for (const excluded of excludedRelativePaths) {
        if (relative === excluded || relative.startsWith(`${excluded}/`)) {
            return true;
        }
    }
    return false;
}

function generateIndex(dir: string) {
    const entries = fs.readdirSync(dir);
    const files: string[] = [];
    const folders: string[] = [];

    for (const entry of entries) {
        const fullPath = path.join(dir, entry);
        if (isExcluded(fullPath)) continue;

        const stat = fs.statSync(fullPath);
        if (stat.isDirectory()) {
            folders.push(entry);
        } else if (
            (entry.endsWith('.ts') || entry.endsWith('.tsx')) &&
            !entry.endsWith('.d.ts')
        ) {
            files.push(entry);
        }
    }

    for (const folder of folders) {
        generateIndex(path.join(dir, folder));
    }

    const exportFiles = files.map((f) => {
        const name = path.parse(f).name;
        return `export * from './${name}';`;
    });

    const importFolders = folders.map((f) => {
        const pascalName = toPascalCase(f);
        return `import * as ${pascalName} from './${f}';`;
    });

    const exportFolders = folders.map((f) => {
        const pascalName = toPascalCase(f);
        return `  ${pascalName},`;
    });

    const content =
        importFolders.join('\n') +
        (importFolders.length > 0 && exportFiles.length > 0 ? '\n\n' : '') +
        exportFiles.join('\n') +
        (folders.length > 0
            ? `\n\nexport {\n${exportFolders.join('\n')}\n};\n`
            : '\n');

    fs.writeFileSync(path.join(dir, 'index.ts'), content);
}

generateIndex(ROOT_DIR);
