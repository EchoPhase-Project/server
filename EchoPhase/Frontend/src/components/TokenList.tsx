import { createSignal, onMount } from 'solid-js';
import * as DiscordTokenPb from '../proto/discord_token_pb';
import client from '../lib/grpcClient';

export default function TokenList() {
  const [tokens, setTokens] = createSignal<DiscordTokenPb.DiscordTokenGrpc.AsObject[]>([]);

  onMount(() => {
    const request = new DiscordTokenPb.DiscordTokenSearchOptionsGrpc();
    request.setUserIdsList(['a424d119-dc1f-458b-a316-b673f6f4875a']);

    client.get(request, {}, (err, response) => {
      if (err) {
        console.error('gRPC Error:', err.message);
        return;
      }

      const list = response.getItemsList().map(item => item.toObject());
	  console.log(list);
      setTokens(list);
    });
  });

  return (
    <ul>
      {tokens().map(token => (
        <li>
          <strong>{token.name}</strong> — {token.token}
        </li>
      ))}
    </ul>
  );
}
