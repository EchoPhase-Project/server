export class IdManager {
    private nextId = 1;
    private freeIds: number[] = [];

    create(): number {
        if (this.freeIds.length > 0) {
            // Повторно используем удалённый id
            return this.freeIds.pop()!;
        }
        return this.nextId++;
    }

    release(id: number): void {
        if (id < this.nextId && !this.freeIds.includes(id)) {
            this.freeIds.push(id);
        }
    }
}

export default IdManager;
