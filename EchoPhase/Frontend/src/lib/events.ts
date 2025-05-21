import * as signalR from "@microsoft/signalr";

export class Events {
    private static shutdown = false;

    static connection = new signalR.HubConnectionBuilder()
        .withUrl("/eventHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    static {
        this.connection.start()
            .then((): void => {
                console.log("Connection established.");
            })
            .catch((err: any): void => {
				console.error(err)
				this.handleConnectionError(err);
			});

        // Listen for connection closure
        this.connection.onclose(async (error?: any): Promise<void> => {
            if (this.shutdown)
			{
                console.log("Connection closed due to server shutdown. No reconnection attempt.");
				return;
			}

			if (error && error.statusCode >= 500) {
				console.error("Connection closed due to server error. Shutting down.", error);
				this.shutdown = true;
			}

			try {
				console.log("Attempting to reconnect...");
				await this.connection.start();
			} catch (err) {
				console.error("Failed to reconnect after connection closed", err);
				this.shutdown = true;
			}
        });

        // Listen for the shutdown notification from the server
        this.connection.on("ShutdownNotification", (message: string) => {
            console.log(message);
            this.shutdown = true;
            this.connection.stop().then(() => {
                console.log("Connection stopped due to server shutdown.");
            });
        });
    }

	private static handleConnectionError(err: any): void {
		if (err && err.statusCode >= 500) {
			this.shutdown = true;
			console.error("Failed to start connection due to server error.", err);
		} else {
			console.error("Failed to start connection:", err);
		}
	}
}

export default Events;
