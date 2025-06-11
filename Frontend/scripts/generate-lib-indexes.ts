import fs from 'fs';
import path from 'path';

function toPascalCase(str: string) {
    return str.replace(/(^\w|-\w)/g, (m) => m.replace('-', '').toUpperCase());
}

function generateIndex(dir: string) {
    const entries = fs.readdirSync(dir);
    const files: string[] = [];
    const folders: string[] = [];

    for (const entry of entries) {
        if (entry === 'index.ts') continue;

        const fullPath = path.join(dir, entry);
        if (fs.statSync(fullPath).isDirectory()) {
            folders.push(entry);
        } else if (entry.endsWith('.ts') || entry.endsWith('.tsx')) {
            files.push(entry);
        }
    }

    // Рекурсивно генерируем index.ts для вложенных папок
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

// Запускаем для src/lib
const LIB_PATH = path.resolve(__dirname, '../src/lib');
generateIndex(LIB_PATH);
