import { FiHome } from 'solid-icons/fi';
import Head from "./Head";

const Home = () => (
  <div>
	<Head title="Home Page - SolidJS" description="Welcome to the SolidJS Home Page" />
    <h1>Home Page</h1>
    <FiHome style={{ 'font-size': '48px', color: 'green' }} />
  </div>
);

export default Home;
